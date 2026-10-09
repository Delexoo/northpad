using Northpad.Core.Models;

namespace Northpad.Core.Modules;

public static class KnownModules
{
    public const string Notes = "notes";
    public const string Todo = "todo";
    public const string Calendar = "calendar";
    public const string Reminders = "reminders";
    public const string Mail = "mail";
    public const string Browser = "browser";
    public const string Photos = "photos";
    public const string Maps = "maps";
    public const string WebSearch = "websearch";
    public const string Video = "video";
    public const string Passwords = "passwords";
    public const string Drive = "drive";
    public const string Sheets = "sheets";
    public const string Translate = "translate";
    public const string Wallet = "wallet";

    public static IReadOnlyList<ModuleDescriptor> All { get; } =
    [
        new(Notes, "Notes", "Blocks, lists, and pictures on this computer."),
        new(Todo, "Todo", "Tasks with due dates."),
        new(Calendar, "Calendar", "Events stored here. No outside calendar."),
        new(Reminders, "Reminders", "Alerts while Northpad is open."),
        new(Mail, "Mail", "Messages kept here. Nothing is sent."),
        new(Browser, "Browser", "Pages you open, after you allow network."),
        new(Photos, "Photos", "Pictures encrypted on this computer."),
        new(Maps, "Maps", "OpenStreetMap, after you allow network."),
        new(WebSearch, "Search", "SearXNG, after you allow network."),
        new(Video, "YouTube", "Open-source player. No Google account."),
        new(Passwords, "Passwords", "Secrets encrypted in the vault."),
        new(Drive, "Drive", "Files encrypted on this computer."),
        new(Sheets, "Sheets", "Grids stored on this computer."),
        new(Translate, "Translate", "A glossary. Online only if you add your own server."),
        new(Wallet, "Wallet", "A private ledger. No cards or banks."),
    ];
}

public static class FileKinds
{
    public const string Photo = "photo";
    public const string Drive = "drive";
    public const string NoteImage = "noteimage";
}

public static class DocumentKinds
{
    public const string Calendar = "calendar";
    public const string Reminder = "reminder";
    public const string Mail = "mail";
    public const string Password = "password";
    public const string Sheet = "sheet";
    public const string Wallet = "wallet";
    public const string Glossary = "glossary";
}
