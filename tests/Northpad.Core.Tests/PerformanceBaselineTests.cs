using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Northpad.Core.Cryptography;
using Northpad.Core.Storage;

namespace Northpad.Core.Tests;

public class PerformanceBaselineTests
{
    [Fact]
    public void Measure_encryption_and_note_inserts()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes(new string('n', 2048));
        var aad = "perf"u8.ToArray();
        var encrypt = Stopwatch.StartNew();
        for (var i = 0; i < 200; i++)
        {
            _ = AesGcmCipher.Encrypt(key, plaintext, aad);
        }

        encrypt.Stop();

        using var workspace = new TestWorkspace();
        workspace.Vault.EnsureCreated();
        workspace.Vault.UnlockWithWindowsAccount();
        var notes = new NoteRepository(workspace.Database, workspace.Vault.Protector);
        var insert = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
        {
            notes.Create("Note " + i, new string('x', 1024));
        }

        insert.Stop();
        var list = Stopwatch.StartNew();
        var loaded = notes.List();
        list.Stop();

        Assert.Equal(50, loaded.Count);
        Assert.True(encrypt.Elapsed.TotalSeconds < 30);
        Assert.True(insert.Elapsed.TotalSeconds < 30);
        Console.WriteLine(
            $"PERF encrypt200x2kb={encrypt.Elapsed.TotalMilliseconds:F1}ms insert50x1kb={insert.Elapsed.TotalMilliseconds:F1}ms list50={list.Elapsed.TotalMilliseconds:F1}ms");
    }
}
