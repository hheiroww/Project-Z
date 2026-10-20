# Offline textbox spellchecking

Native `Textbox` inputs (including `PlaceholderTextbox`, derived inputs and
compatibility textboxes) check spelling automatically. Misspelled words have red
squiggles. Right-click a marked word to choose one of up to five suggestions or
**Add to dictionary**. Cut, Copy, Paste and Select all remain available.
Corrections replace only the clicked word and return focus to the input.

Spelling uses the local Windows spelling engine through WPF, on a dedicated
background STA thread. It requires a Windows spelling language installed on the
computer; no network service, account or download is used while checking text.
The default language is the current culture (`en-US` for an invariant culture).
Set `SpellCheckLanguage` to a language tag such as `en-US` or `en-GB`. Unsupported
languages may provide no errors or suggestions. `SpellCheckError` exposes engine
or dictionary IO failures without interrupting typing.

Checks begin after the text has been stable for 250 ms. Superseded checks are
cancelled and their results discarded. Underlines use the input's selection
layout, so wrapping, alignment, scrolling and clipping follow the drawn text.
Password boxes, masked inputs and read-only inputs are excluded. For identifiers,
paths, code or other non-prose inputs, set `IsSpellCheckEnabled = False`.

## Custom dictionaries

By default all textboxes in an application share a case-insensitive word list:

`%LOCALAPPDATA%\ProjectZ\Spelling\<entry assembly name>\dictionary.txt`

This file contains UTF-8 text, one accepted word per line. Empty lines and lines
starting with `#` are ignored. **Add to dictionary** saves the word before accepting
it and immediately invalidates spelling results in every input using that list.
Only words explicitly added by the user are persisted; input contents are never
written to disk. These accepted words supplement the installed language dictionary;
suggestions come from the Windows engine.

Applications may use another file and share it among selected inputs:

```vb
Dim dictionary = New OfflineSpellChecker(customDictionaryPath)
textbox.SpellChecker = dictionary
textbox.SpellCheckLanguage = "en-US"
dictionary.AddWord("ProjectZ")
```

Edit the file to add or remove words, then call `dictionary.ReloadDictionary()`
(or restart the application). Use `OfflineSpellChecker.Default.DictionaryPath`
to locate the default list. A failed save leaves the word unaccepted; applications
can display `textbox.SpellCheckError` in their own settings/status UI.

## Runtime regression probe

```powershell
dotnet run --project tests/SpellChecking/SpellChecking.csproj -c Release -p:GeneratePackageOnBuild=false -- artifacts/SpellChecking
```

The native DX12 fixture uses the real local spelling engine, routed pointer
events, persistent temporary dictionaries and rendered pixel checks. It writes
results and screenshots under the supplied output directory, which is not source.
