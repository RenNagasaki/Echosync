using System.Collections.Generic;
using Dalamud.Game;

namespace Echosync.Localization;

public static class Loc
{
    private static readonly Dictionary<string, Dictionary<ClientLanguage, string>> Translations = new()
    {
        ["Enabled"] = new()
        {
            [ClientLanguage.German] = "Aktiviert",
            [ClientLanguage.French] = "Activ\u00e9",
            [ClientLanguage.Japanese] = "\u6709\u52b9",
        },
        ["Only special NPCs (Any marker above head)"] = new()
        {
            [ClientLanguage.German] = "Nur besondere NPCs (Markierung \u00fcber dem Kopf)",
            [ClientLanguage.French] = "NPCs sp\u00e9ciaux uniquement (marqueur au-dessus de la t\u00eate)",
            [ClientLanguage.Japanese] = "\u7279\u5225\u306aNPC\u306e\u307f\uff08\u982d\u4e0a\u306e\u30de\u30fc\u30ab\u30fc\uff09",
        },
        ["Connect at start"] = new()
        {
            [ClientLanguage.German] = "Beim Start verbinden",
            [ClientLanguage.French] = "Connexion au d\u00e9marrage",
            [ClientLanguage.Japanese] = "\u8d77\u52d5\u6642\u306b\u63a5\u7d9a",
        },
        ["Sync server"] = new()
        {
            [ClientLanguage.German] = "Sync-Server",
            [ClientLanguage.French] = "Serveur de synchronisation",
            [ClientLanguage.Japanese] = "\u540c\u671f\u30b5\u30fc\u30d0\u30fc",
        },
        ["Sync channel"] = new()
        {
            [ClientLanguage.German] = "Sync-Kanal",
            [ClientLanguage.French] = "Canal de synchronisation",
            [ClientLanguage.Japanese] = "\u540c\u671f\u30c1\u30e3\u30f3\u30cd\u30eb",
        },
        ["Sync password"] = new()
        {
            [ClientLanguage.German] = "Sync-Passwort",
            [ClientLanguage.French] = "Mot de passe de synchronisation",
            [ClientLanguage.Japanese] = "\u540c\u671f\u30d1\u30b9\u30ef\u30fc\u30c9",
        },
        ["Connect"] = new()
        {
            [ClientLanguage.German] = "Verbinden",
            [ClientLanguage.French] = "Connecter",
            [ClientLanguage.Japanese] = "\u63a5\u7d9a",
        },
        ["Disconnect"] = new()
        {
            [ClientLanguage.German] = "Trennen",
            [ClientLanguage.French] = "D\u00e9connecter",
            [ClientLanguage.Japanese] = "\u5207\u65ad",
        },
        ["Show debug logs"] = new()
        {
            [ClientLanguage.German] = "Debug-Logs anzeigen",
            [ClientLanguage.French] = "Afficher les logs de d\u00e9bogage",
            [ClientLanguage.Japanese] = "\u30c7\u30d0\u30c3\u30b0\u30ed\u30b0\u3092\u8868\u793a",
        },
        ["Show error logs"] = new()
        {
            [ClientLanguage.German] = "Fehler-Logs anzeigen",
            [ClientLanguage.French] = "Afficher les logs d'erreurs",
            [ClientLanguage.Japanese] = "\u30a8\u30e9\u30fc\u30ed\u30b0\u3092\u8868\u793a",
        },
        ["Always jump to bottom"] = new()
        {
            [ClientLanguage.German] = "Immer nach unten springen",
            [ClientLanguage.French] = "Toujours aller en bas",
            [ClientLanguage.Japanese] = "\u5e38\u306b\u4e0b\u90e8\u3078\u30b8\u30e3\u30f3\u30d7",
        },
        ["Show ID: 0"] = new()
        {
            [ClientLanguage.German] = "ID: 0 anzeigen",
            [ClientLanguage.French] = "Afficher ID : 0",
            [ClientLanguage.Japanese] = "ID: 0 \u3092\u8868\u793a",
        },
        ["Show ID 0 entries"] = new()
        {
            [ClientLanguage.German] = "ID-0-Eintr\u00e4ge anzeigen",
            [ClientLanguage.French] = "Afficher les entr\u00e9es ID 0",
            [ClientLanguage.Japanese] = "ID 0 \u306e\u30a8\u30f3\u30c8\u30ea\u30fc\u3092\u8868\u793a",
        },
        ["General"] = new()
        {
            [ClientLanguage.German] = "Allgemein",
            [ClientLanguage.French] = "G\u00e9n\u00e9ral",
            [ClientLanguage.Japanese] = "\u4e00\u822c",
        },
        ["Logs"] = new()
        {
            [ClientLanguage.German] = "Protokolle",
            [ClientLanguage.French] = "Journaux",
            [ClientLanguage.Japanese] = "\u30ed\u30b0",
        },
        ["Fakeuser"] = new()
        {
            [ClientLanguage.German] = "Testbenutzer",
            [ClientLanguage.French] = "Utilisateur fictif",
            [ClientLanguage.Japanese] = "\u30c6\u30b9\u30c8\u30e6\u30fc\u30b6\u30fc",
        },
        ["Options:"] = new()
        {
            [ClientLanguage.German] = "Optionen:",
            [ClientLanguage.French] = "Options :",
            [ClientLanguage.Japanese] = "\u30aa\u30d7\u30b7\u30e7\u30f3:",
        },
        ["Log:"] = new()
        {
            [ClientLanguage.German] = "Protokoll:",
            [ClientLanguage.French] = "Journal :",
            [ClientLanguage.Japanese] = "\u30ed\u30b0:",
        },
        ["Enter Dialogue"] = new()
        {
            [ClientLanguage.German] = "Dialog betreten",
            [ClientLanguage.French] = "Entrer dans le dialogue",
            [ClientLanguage.Japanese] = "\u4f1a\u8a71\u306b\u5165\u308b",
        },
        ["Exit Dialogue"] = new()
        {
            [ClientLanguage.German] = "Dialog verlassen",
            [ClientLanguage.French] = "Quitter le dialogue",
            [ClientLanguage.Japanese] = "\u4f1a\u8a71\u3092\u7d42\u4e86",
        },
        ["Request Advance"] = new()
        {
            [ClientLanguage.German] = "Weiter anfordern",
            [ClientLanguage.French] = "Demander l'avancement",
            [ClientLanguage.Japanese] = "\u9032\u884c\u3092\u8981\u6c42",
        },
        ["Filter Options"] = new()
        {
            [ClientLanguage.German] = "Filteroptionen",
            [ClientLanguage.French] = "Options de filtre",
            [ClientLanguage.Japanese] = "\u30d5\u30a3\u30eb\u30bf\u30fc\u30aa\u30d7\u30b7\u30e7\u30f3",
        },
        ["No log entries."] = new()
        {
            [ClientLanguage.German] = "Keine Protokolleintr\u00e4ge.",
            [ClientLanguage.French] = "Aucune entr\u00e9e de journal.",
            [ClientLanguage.Japanese] = "\u30ed\u30b0\u30a8\u30f3\u30c8\u30ea\u30fc\u304c\u3042\u308a\u307e\u305b\u3093\u3002",
        },
        ["Configuration"] = new()
        {
            [ClientLanguage.German] = "Konfiguration",
            [ClientLanguage.French] = "Configuration",
            [ClientLanguage.Japanese] = "\u8a2d\u5b9a",
        },
    };

    private static ClientLanguage _language = ClientLanguage.English;

    public static void Initialize(ClientLanguage language) => _language = language;

    public static string S(string english)
    {
        if (_language == ClientLanguage.English) return english;
        if (Translations.TryGetValue(english, out var translations) && translations.TryGetValue(_language, out var translated))
            return translated;
        return english;
    }
}
