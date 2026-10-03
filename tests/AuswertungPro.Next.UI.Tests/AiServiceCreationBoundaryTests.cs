using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Friert echte Konstruktoraufrufe der vier KI-Dienste in UI/Ai und UI/Services ein.
/// Die Baseline nennt den aeussersten UI-Typ, Dienst und Anzahl. Dadurch zaehlen
/// auch Aufrufe in Lambdas und asynchronen Zustandsmaschinen zum Besitzer.
/// </summary>
public sealed class AiServiceCreationBoundaryTests
{
    private static readonly HashSet<Type> GeschuetzteDienste =
    [
        typeof(OllamaClient),
        typeof(KnowledgeBaseContext),
        typeof(EmbeddingService),
        typeof(KnowledgeBaseManager),
    ];

    // Format: "<vollstaendiger Name des aeussersten UI-Typs> :: <Dienst> :: <Anzahl>".
    private static readonly HashSet<string> ErlaubterBestand = new(StringComparer.Ordinal)
    {
        "AuswertungPro.Next.UI.Ai.Coding.CodingAiRuntimeFactory :: OllamaClient :: 1",
        "AuswertungPro.Next.UI.Ai.Coding.CodingOsdMeterService :: OllamaClient :: 1",
        "AuswertungPro.Next.UI.Ai.Live.LiveDetectionRuntimeFactory :: OllamaClient :: 1",
        "AuswertungPro.Next.UI.Ai.Training.SelfTrainingSessionController :: OllamaClient :: 1",
        "AuswertungPro.Next.UI.Ai.Training.SelfTrainingSessionController :: KnowledgeBaseContext :: 1",
        "AuswertungPro.Next.UI.Ai.Training.SelfTrainingSessionController :: EmbeddingService :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKbIndexSession :: KnowledgeBaseContext :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKbIndexSession :: EmbeddingService :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKbIndexSession :: KnowledgeBaseManager :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKnowledgeBaseSampleDeindexer :: KnowledgeBaseContext :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKnowledgeBaseSampleDeindexer :: EmbeddingService :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingKnowledgeBaseSampleDeindexer :: KnowledgeBaseManager :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingMeterTimelineServiceFactory :: OllamaClient :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingReviewFeedbackServiceFactory :: EmbeddingService :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingReviewFeedbackServiceFactory :: KnowledgeBaseManager :: 1",
        "AuswertungPro.Next.UI.Ai.Training.TrainingSelectedReviewRuntime :: KnowledgeBaseContext :: 3",
        "AuswertungPro.Next.UI.Services.TrainingStudioWindowDependencyFactory :: OllamaClient :: 1",
    };

    private static readonly OpCode[] EinByte = new OpCode[256];
    private static readonly OpCode[] ZweiByte = new OpCode[256];

    static AiServiceCreationBoundaryTests()
    {
        foreach (var feld in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (feld.GetValue(null) is not OpCode opcode) continue;
            var wert = unchecked((ushort)opcode.Value);
            if (opcode.Size == 1) EinByte[wert] = opcode;
            else if (opcode.Size == 2) ZweiByte[wert & 0xff] = opcode;
        }
    }

    [Fact]
    public void Ui_Ai_und_Services_erzeugen_keine_zusaetzlichen_Ki_Dienste()
    {
        var assembly = typeof(AuswertungPro.Next.UI.ServiceProvider).Assembly;
        var aktuell = ZaehleErzeugungen(assembly, type => GeschuetzterUiTyp(type));
        var neue = aktuell.Except(ErlaubterBestand).OrderBy(x => x).ToArray();
        var veraltete = ErlaubterBestand.Except(aktuell).OrderBy(x => x).ToArray();

        Assert.True(neue.Length == 0,
            "Neue direkte KI-Dienst-Erzeugung im kompilierten UI-Code gefunden:\n  " +
            string.Join("\n  ", neue));
        Assert.True(veraltete.Length == 0,
            "Alt-Erzeugung entfernt: Baseline entsprechend verkleinern:\n  " +
            string.Join("\n  ", veraltete));
    }

    [Fact]
    public void Probe_erkennt_Konstruktoraufrufe_auch_in_compilererzeugter_Lambda()
    {
        var vorher = ZaehleErzeugungen(typeof(ProbeklasseMitEinemAufruf).Assembly,
            type => type == typeof(ProbeklasseMitEinemAufruf));
        var nachher = ZaehleErzeugungen(typeof(Probeklasse).Assembly,
            type => type == typeof(Probeklasse) || IstInProbeklasseVerschachtelt(type));

        Assert.Contains(
            $"{typeof(AiServiceCreationBoundaryTests).FullName} :: KnowledgeBaseContext :: 1",
            vorher);
        Assert.Contains(
            $"{typeof(AiServiceCreationBoundaryTests).FullName} :: KnowledgeBaseContext :: 2",
            nachher);
    }

    private static bool GeschuetzterUiTyp(Type type)
    {
        var name = AeussersterTyp(type).Namespace;
        return name is not null &&
            (name == "AuswertungPro.Next.UI.Ai" || name.StartsWith("AuswertungPro.Next.UI.Ai.", StringComparison.Ordinal) ||
             name == "AuswertungPro.Next.UI.Services" || name.StartsWith("AuswertungPro.Next.UI.Services.", StringComparison.Ordinal));
    }

    private static bool IstInProbeklasseVerschachtelt(Type type)
    {
        for (var eltern = type.DeclaringType; eltern is not null; eltern = eltern.DeclaringType)
            if (eltern == typeof(Probeklasse)) return true;
        return false;
    }

    private static Type AeussersterTyp(Type type)
    {
        while (type.DeclaringType is not null) type = type.DeclaringType;
        return type;
    }

    private static HashSet<string> ZaehleErzeugungen(Assembly assembly, Func<Type, bool> typAuswahl)
    {
        var anzahl = new Dictionary<(Type Besitzer, Type Dienst), int>();
        foreach (var type in assembly.GetTypes().Where(typAuswahl))
        {
            foreach (var methode in MethodenMitKoerper(type))
            {
                foreach (var dienst in Konstruktoraufrufe(methode))
                {
                    var schluessel = (AeussersterTyp(type), dienst);
                    anzahl[schluessel] = anzahl.GetValueOrDefault(schluessel) + 1;
                }
            }
        }

        return anzahl.Select(eintrag =>
                $"{eintrag.Key.Besitzer.FullName} :: {eintrag.Key.Dienst.Name} :: {eintrag.Value}")
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<MethodBase> MethodenMitKoerper(Type type)
    {
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                   BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var gesehen = new HashSet<int>();
        foreach (var methode in type.GetMethods(flags))
            if (gesehen.Add(methode.MetadataToken)) yield return methode;
        foreach (var konstruktor in type.GetConstructors(flags))
            if (gesehen.Add(konstruktor.MetadataToken)) yield return konstruktor;
        if (type.TypeInitializer is { } statischerKonstruktor &&
            gesehen.Add(statischerKonstruktor.MetadataToken))
            yield return statischerKonstruktor;
    }

    private static IEnumerable<Type> Konstruktoraufrufe(MethodBase methode)
    {
        var il = methode.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;

        var position = 0;
        while (position < il.Length)
        {
            var opcode = il[position] == 0xfe
                ? ZweiByte[il[++position]]
                : EinByte[il[position]];
            position++;
            if (opcode.Size == 0)
                throw new InvalidOperationException($"Unbekannter IL-Befehl in {methode}.");

            var laenge = OperandLaenge(opcode.OperandType, il, position);
            if (laenge < 0 || laenge > il.Length - position)
                throw new InvalidOperationException($"Ungueltiger IL-Operand in {methode}.");

            if (opcode == OpCodes.Newobj)
            {
                var token = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(position, 4));
                MethodBase aufruf;
                try
                {
                    aufruf = methode.Module.ResolveMethod(token,
                        methode.DeclaringType?.GetGenericArguments(),
                        methode.IsGenericMethod ? methode.GetGenericArguments() : null)
                        ?? throw new InvalidOperationException($"Konstruktor-Token {token} ohne Ziel.");
                }
                catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
                {
                    throw new InvalidOperationException(
                        $"Konstruktor-Token {token} in {methode} konnte nicht aufgeloest werden.", ex);
                }

                if (aufruf is not ConstructorInfo)
                    throw new InvalidOperationException($"newobj-Token {token} in {methode} ist kein Konstruktor.");
                if (aufruf.DeclaringType is { } dienst && GeschuetzteDienste.Contains(dienst))
                    yield return dienst;
            }

            position += laenge;
        }
    }

    private static int OperandLaenge(OperandType operand, byte[] il, int position)
        => operand switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
                OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch when position <= il.Length - 4 =>
                checked(4 + 4 * BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(position, 4))),
            _ => throw new InvalidOperationException($"Unbekannter IL-Operandtyp {operand}."),
        };

    private static class Probeklasse
    {
        public static KnowledgeBaseContext Direkt() => new();
        public static Func<KnowledgeBaseContext> InLambda() => static () => new();
        public static string NurText() => "new KnowledgeBaseContext()";
    }

    private static class ProbeklasseMitEinemAufruf
    {
        public static KnowledgeBaseContext Direkt() => new();
    }
}
