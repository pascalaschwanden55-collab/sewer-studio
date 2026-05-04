// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Phase 2.2: Zentraler Writer fuer die KnowledgeBase.
///
/// Buendelt alle schreibenden Zugriffe auf die SQLite-DB hinter einem
/// gemeinsamen Lock (SemaphoreSlim). Ergaenzt die in
/// <see cref="KnowledgeBaseContext"/> bereits gesetzten PRAGMAs
/// (foreign_keys=ON, journal_mode=WAL, busy_timeout=5000, synchronous=NORMAL)
/// um eine prozessweite Serialisierung der Schreibpfade.
///
/// Aufrufer koennen weiter <see cref="KnowledgeBaseContext.Connection"/>
/// fuer reines Lesen nutzen. Sobald ein Schreibvorgang ansteht, MUSS er
/// ueber diesen Writer laufen, damit
///   1) parallele Inserts / Updates serialisiert sind und
///   2) Transaktionen reentrancy-sicher mit dem gleichen Lock laufen.
///
/// Bewusst KEIN Channel(T) und KEINE Producer/Consumer-Pipeline — das ist
/// Phase 2.3. Hier nur die zentrale Lock-Schicht und Convenience-Helper.
/// </summary>
public sealed class KnowledgeBaseWriter : IDisposable
{
    private readonly KnowledgeBaseContext _db;
    private readonly SemaphoreSlim _writeLock = new(initialCount: 1, maxCount: 1);
    private bool _disposed;

    public KnowledgeBaseWriter(KnowledgeBaseContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>Direkter Lese-Zugriff auf die DB-Verbindung. KEINE Schreibvorgaenge!</summary>
    public KnowledgeBaseContext Db => _db;

    // ── Synchrone Schreib-Pfade ────────────────────────────────────────────

    /// <summary>
    /// Fuehrt eine schreibende Aktion unter Lock aus.
    /// </summary>
    public void Execute(Action<KnowledgeBaseContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _writeLock.Wait();
        try
        {
            action(_db);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Fuehrt eine schreibende Aktion mit Rueckgabewert unter Lock aus.
    /// </summary>
    public T Execute<T>(Func<KnowledgeBaseContext, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _writeLock.Wait();
        try
        {
            return action(_db);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Fuehrt eine Aktion innerhalb einer Transaktion unter Lock aus.
    /// Bei Exception erfolgt Rollback, sonst Commit.
    /// </summary>
    public void ExecuteInTransaction(Action<SqliteConnection, SqliteTransaction> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Execute(db =>
        {
            using var tx = db.Connection.BeginTransaction();
            try
            {
                action(db.Connection, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    // ── Asynchrone Schreib-Pfade ───────────────────────────────────────────

    /// <summary>
    /// Async-Variante von <see cref="Execute(Action{KnowledgeBaseContext})"/>.
    /// </summary>
    public async Task ExecuteAsync(
        Func<KnowledgeBaseContext, Task> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await action(_db).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Async-Variante mit Rueckgabewert.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<KnowledgeBaseContext, Task<T>> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await action(_db).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writeLock.Dispose();
    }
}
