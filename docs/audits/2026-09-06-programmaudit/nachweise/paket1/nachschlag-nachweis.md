# Nachschlag-Test: namentlicher Nachweis

Auszug aus der bereits gespeicherten Datei `../ui-rerun.log`. Der Testname und das 60-Sekunden-Limit stehen hier zusammen. Die zusätzliche Einzelwiederholung wurde vom Nutzer mitgeteilt; dafür wird kein eigener, nicht vorliegender Lauf behauptet.

```text
AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern [FAIL]
  Fehler AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern [1 m 1 s]
  Fehlermeldung:
   Isolierter WPF-Test fehlgeschlagen (Exit-Code 1; Szenario-Bestaetigung fehlt).

stdout:
VSTest-Version 18.0.2 (x64)

Die Testausf├╝hrung wird gestartet, bitte warten...
Insgesamt 1 Testdateien stimmten mit dem angegebenen Muster ├╝berein.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.7+8f2703126a (64-bit .NET 10.0.11)
[xUnit.net 00:00:00.11]   Discovering: AuswertungPro.Next.UI.Tests
[xUnit.net 00:00:00.40]   Discovered:  AuswertungPro.Next.UI.Tests
[xUnit.net 00:00:00.40]   Starting:    AuswertungPro.Next.UI.Tests
[xUnit.net 00:01:00.45]       Der WPF-Test wurde nicht innerhalb von 60 Sekunden beendet. Er kann blockiert sein oder unter hoher Systemlast zu langsam laufen.
[xUnit.net 00:01:00.45]       Stack Trace:
[xUnit.net 00:01:00.45]         C:\Sewer-Studio_KI_4.5\tests\AuswertungPro.Next.UI.Tests\StaTestRunner.cs(36,0): at AuswertungPro.Next.UI.Tests.StaTestRunner.Run(Action action, Nullable`1 timeout)
[xUnit.net 00:01:00.45]         C:\Sewer-Studio_KI_4.5\tests\AuswertungPro.Next.UI.Tests\NachschlagKontextmenueTests.cs(65,0): at AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues()
[xUnit.net 00:01:00.45]            at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
[xUnit.net 00:01:00.45]            at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
[xUnit.net 00:01:00.45]   Finished:    AuswertungPro.Next.UI.Tests
  Fehler AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues [1 m]
  Fehlermeldung:
   Der WPF-Test wurde nicht innerhalb von 60 Sekunden beendet. Er kann blockiert sein oder unter hoher Systemlast zu langsam laufen.
  Stapelverfolgung:
     at AuswertungPro.Next.UI.Tests.StaTestRunner.Run(Action action, Nullable`1 timeout) in C:\Sewer-Studio_KI_4.5\tests\AuswertungPro.Next.UI.Tests\StaTestRunner.cs:line 36
   at AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues() in C:\Sewer-Studio_KI_4.5\tests\AuswertungPro.Next.UI.Tests\NachschlagKontextmenueTests.cs:line 65
   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Gesamtzahl Tests: 1
     Nicht bestanden: 1
 Gesamtzeit: 1.0139 Minuten


stderr:
[xUnit.net 00:01:00.45]     AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues [FAIL]
Fehler beim Testlauf.

  Stapelverfolgung:
     at AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern() in C:\Sewer-Studio_KI_4.5\tests\AuswertungPro.Next.UI.Tests\NachschlagKontextmenueTests.cs:line 58
--- End of stack trace from previous location ---
[xUnit.net 00:01:03.48]     AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues [SKIP]
  Übersprungen AuswertungPro.Next.UI.Tests.NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues [1 ms]
Ergebnisdatei: C:\Sewer-Studio_KI_4.5\.tmp\programmaudit-2026-09-06\reruns\ui-rerun.trx

Fehler!      : Fehler:     2, erfolgreich:     9, übersprungen:     1, gesamt:    12, Dauer: 1 m 3 s - AuswertungPro.Next.UI.Tests.dll (net10.0)

```
