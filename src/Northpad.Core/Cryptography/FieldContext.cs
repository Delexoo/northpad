namespace Northpad.Core.Cryptography;

public static class FieldContext
{
    public static string NoteTitle(Guid id) => $"northpad:note:title:v1:{id:D}";

    public static string NoteBody(Guid id) => $"northpad:note:body:v1:{id:D}";

    public static string TaskTitle(Guid id) => $"northpad:task:title:v1:{id:D}";

    public static string TaskDetails(Guid id) => $"northpad:task:details:v1:{id:D}";

    public static string Document(string kind, Guid id) => $"northpad:doc:{kind}:v1:{id:D}";

    public static string FileName(Guid id) => $"northpad:file:name:v1:{id:D}";

    public static string FileBody(Guid id) => $"northpad:file:body:v1:{id:D}";

    public static ReadOnlySpan<byte> PassphraseWrap => "northpad:kek:passphrase:v1"u8;

    public static ReadOnlySpan<byte> RecoveryWrap => "northpad:kek:recovery:v1"u8;
}
