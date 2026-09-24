# ZeroZip Community Language Packages

ZeroZip supports drop-in language packs. You can add a new language without recompiling the application by placing a `.json` file in this folder.

## File Format

```json
{
  "code": "fr",
  "displayName": "🇫🇷 French",
  "nativeName": "Français",
  "cultureName": "fr-FR",
  "translations": {
    "Menu_File": "Fichier",
    "Menu_OpenArchive": "Ouvrir l'archive... (Ctrl+O)",
    "Menu_Exit": "Quitter (Alt+F4)"
  }
}
```

## Fallback
If any key is missing from your custom `.json` file, ZeroZip automatically falls back to **English (`en`)**, then **Vietnamese (`vi`)**, ensuring no UI text ever breaks.
