namespace MarkdownPreview;

/// <summary>Provides all application labels in Polish and English.</summary>
public static class Strings
{
    private static readonly Dictionary<string, (string Pl, string En)> Labels = new()
    {
        ["Editor"] = ("EDYTOR", "EDITOR"), ["Preview"] = ("PODGLĄD MARKDOWN", "MARKDOWN PREVIEW"),
        ["Retry"] = ("Ponów podgląd", "Retry preview"),
        ["PreviewFailed"] = ("Nie udało się wyświetlić podglądu.", "Preview could not be displayed."),
        ["ZoomHint"] = ("Ctrl + kółko myszy: przybliżenie · Ctrl+0: 100%", "Ctrl + mouse wheel: zoom · Ctrl+0: 100%"),
        ["File"] = ("_Plik", "_File"), ["New"] = ("_Nowy", "_New"), ["Window"] = ("Nowe _okno", "New _window"),
        ["Open"] = ("_Otwórz…", "_Open…"), ["Save"] = ("_Zapisz", "_Save"), ["SaveAs"] = ("Zapisz _jako…", "Save _as…"),
        ["Exit"] = ("Za_kończ", "E_xit"), ["Edit"] = ("_Edycja", "_Edit"), ["Undo"] = ("_Cofnij", "_Undo"),
        ["Redo"] = ("_Ponów", "_Redo"), ["Cut"] = ("Wy_tnij", "Cu_t"), ["Copy"] = ("_Kopiuj", "_Copy"),
        ["Paste"] = ("_Wklej", "_Paste"), ["Delete"] = ("_Usuń", "_Delete"), ["Find"] = ("_Znajdź", "_Find"),
        ["Next"] = ("Znajdź następne", "Find next"), ["Previous"] = ("Znajdź poprzednie", "Find previous"),
        ["Replace"] = ("Za_mień", "_Replace"), ["ReplaceAll"] = ("Zamień wszystkie", "Replace all"),
        ["GoTo"] = ("Przejdź do _wiersza…", "_Go to line…"), ["SelectAll"] = ("Zaznacz w_szystko", "Select _all"),
        ["Date"] = ("_Data i godzina", "Time/_date"), ["FileFormat"] = ("Format pliku", "File format"),
        ["Format"] = ("F_ormat", "F_ormat"), ["Wrap"] = ("_Zawijanie wierszy", "_Word wrap"),
        ["Settings"] = ("_Ustawienia", "_Settings"), ["Theme"] = ("Motyw", "Theme"), ["Font"] = ("Czcionka", "Font"),
        ["Size"] = ("Rozmiar", "Size"), ["Language"] = ("Język", "Language"), ["Dark"] = ("Ciemny", "Dark"),
        ["Light"] = ("Jasny", "Light"), ["System"] = ("Systemowy", "System"), ["Untitled"] = ("Bez tytułu", "Untitled"),
        ["Unsaved"] = ("Zapisać zmiany w dokumencie?", "Save changes to this document?"),
        ["External"] = ("Plik na dysku zmienił się lub został usunięty. Nadpisać go bieżącą treścią?", "The file changed on disk or was deleted. Overwrite it with the current text?"),
        ["Reload"] = ("Plik został zmieniony poza aplikacją. Wczytać wersję z dysku?", "The file changed outside the application. Reload from disk?"),
        ["Error"] = ("Błąd", "Error"), ["Line"] = ("Wiersz", "Line"), ["Column"] = ("Kolumna", "Column"),
        ["Errors"] = ("Błędy", "Errors"), ["PreviewUnavailable"] = ("Podgląd wymaga Microsoft Edge WebView2 Runtime. Zainstaluj go i uruchom aplikację ponownie.", "Preview requires Microsoft Edge WebView2 Runtime. Install it and restart the application."),
        ["Loading"] = ("Przygotowywanie podglądu…", "Preparing preview…"), ["NotFound"] = ("Nie znaleziono tekstu.", "Text was not found."),
        ["Text"] = ("Zwykły tekst", "Plain text"), ["AllFiles"] = ("Wszystkie pliki", "All files"),
        ["InvalidLine"] = ("Podaj istniejący numer wiersza.", "Enter an existing line number."),
        ["DiagnosticHint"] = ("Szczegóły błędów są dostępne po najechaniu na ten pasek.", "Hover over this bar for error details.")
    };

    /// <summary>Looks up a localized label by its stable English key.</summary>
    public static string Get(string key, string language) => Labels.TryGetValue(key, out var value) ? language == "pl" ? value.Pl : value.En : key;
}
