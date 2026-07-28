# PassKey — Piano di sviluppo ciclo 2.x (PLAN.md)

> **Versione piano:** 2.0 (ricostruzione) · **Data:** 2026-07-24 · **Stato repo alla stesura:** `main` @ `9ebaa01` (PR #52)
>
> **Perché questo file esiste ed è NEL repo:** il piano originale (`piano-revisione-correzione-miglioramento.md`, 2026-06-02) viveva in `C:\Users\Thomas\.claude\plans\` — fuori dal repository — ed è stato **cancellato dalla retention automatica di Claude Code** (`cleanupPeriodDays`, default 30 giorni). Recupero tentato e fallito. Questo file lo ricostruisce fondendo: handoff di progetto, `analisi/ANALISI-PassKey-2.0.md` (2026-05-30), `analysis_results.md` (audit esterno 2026-05-21), e **ri-verifica puntuale sul codice eseguita il 2026-07-24**. D'ora in poi il piano si aggiorna QUI, via commit.

---

## 0. Regole di processo — INVIOLABILI

### 0.1 Gate di test utente (vale per OGNI cluster/modifica)
Prima di qualunque push/PR/merge:
1. Build locale: `.\scripts\build-installer.ps1` → `Installer\Output\PassKey-Setup-x64.exe` + `PassKey-Portable-x64.zip` (**la build la esegue Claude**, mai l'utente).
2. L'utente installa l'EXE (questo PC + VM Windows 10) e **testa manualmente**: avvio, unlock, funzionalità toccate, zero regressioni.
3. Si attende il **"VIA LIBERA" esplicito dell'utente in chat**. Un "procedi pure" generico NON è il via libera del gate.
4. Solo dopo: commit + push + PR + merge.

> Eccezione ragionevole: modifiche **solo-documentazione** (come questo file) non richiedono l'installer, ma richiedono comunque la revisione e l'OK esplicito dell'utente prima della PR.

### 0.2 Durante un collaudo: si annota, non si implementa
Mentre l'utente sta collaudando una build, **qualunque osservazione, dubbio o idea che emerge va messa in una lista "DA DECIDERE"** — nessuna modifica al codice, nessuna ricompilazione, finché l'utente non dà un **"procedi"** esplicito su quel punto.
**Perché:** modificare il codice a collaudo in corso invalida la build sotto test (l'utente finirebbe per dare il VIA LIBERA a una versione diversa da quella che si pubblica) e costringe a rifare le prove. Errore commesso il 2026-07-28: implementata e ricompilata UX-01 mentre l'utente stava testando, dopo una sua semplice osservazione sul colore di un toast.

### 0.3 Git
- **Linea viva = `main`** (protetto: solo PR + check `test` verde + **squash-merge**).
- Branch di lavoro DA `main`: `fix/2.0/<short>` (o `feat/2.0/<short>` per nuove funzioni).
- Titolo PR: `<type>(<scope>): <descrizione> [v2.0]`.
- Il worktree `cool-wilson-213cae` (`feat/2.0/main`) è **OBSOLETO**: non usarlo (eliminazione = decisione utente, non urgente).

### 0.4 Regola di verifica (estensione del principio "Dubitativo")
- Memoria, informazioni e documentazione locali (questo file incluso) possono essere **errate o datate**: prima di agire, ri-verificare i presupposti su **codice e git**.
- Quando la verifica locale non basta (API esterne, versioni, policy degli store, URL di download), fare **ricerche online** — sempre autorizzate — **multiple, in parallelo, multifattoriali e approfondite**, al **momento dell'implementazione** (non in anticipo: l'informazione decade).
- Checkpoint online già previsti da questo piano: T6.5.6 (API `WinVerifyTrust`, attestazione processo padre, allowlist firmatari browser), T8.1 (URL/versione WindowsAppRuntime), T8.5 (policy correnti Chrome Web Store / Firefox AMO), backlog (stato release CommunityToolkit Labs).

### 0.5 Comandi di riferimento
- Build: `dotnet build src/PassKey.Desktop/PassKey.Desktop.csproj -p:Platform=x64`
- Test: `dotnet test src/PassKey.Tests/PassKey.Tests.csproj` — **baseline: 222 verdi**
- Diagnosi errori XAML mascherati: MSBuild full-framework (vedi memoria di progetto / pitfall XamlCompiler net472).
- Harness sicurezza IPC: `scripts/ipc-security-test.ps1` (a vault SBLOCCATO; attenzione all'auto-lock).

---

## 1. Storico — cosa è stato fatto e perché (chiuso ✅)

| Cluster | Contenuto | PR / commit |
|---|---|---|
| 0–1 | Prep + code quality, DI hardening, `BaseDetailViewModel`, fix XamlRoot dialog | #42 |
| 2 | Sicurezza: ciclo vita DEK (PinnedSecureBuffer), allineamento Argon2id, KDF backup | #44 |
| 3 | TOTP (RFC 6238, import QR/URI/manuale) | #45 |
| 4 | Watchtower: rilevamento password violate HIBP (k-anonymity, opt-in) | #46 |
| 5 | UX, localizzazione completa 6 lingue, polish, **release v2.0.0** | #47 (tag `v2.0.0`) |
| — | Hardening da audit interno giu-2026 (no plaintext IPC, throttling unlock, `.gitignore`, clipboard flush, rotazione backup) — *fuori processo, poi riconciliato* | #51 |
| 6 | Chiusura audit esterno IPC: **SEC-01** (ECDH obbligatorio, errore `ecdh-session-required`), **SEC-02** (sessione legata al `clientId` dell'handshake), **SEC-03** (consenso utente su `get-credential-password`, "Ricorda per questa sessione", reset al lock — **INTERIM**, vedi SEC-05), T6.2 toast autofill, T6.3 indicatore "Connesso", fix deadlock `RPC_E_WRONG_THREAD` nel dialog di consenso. Gate superato con VIA LIBERA. | #52 (`9ebaa01`) |

**Fuori ordine ma già fatto:** release v2.0.0 pubblicata (installer + portable su GitHub Release, **estensioni 1.0.1 live** su Chrome Web Store e Firefox AMO, landing `pass-key.it`). ⚠️ La release corrisponde a `4445f60`: **i fix di sicurezza #51/#52 NON sono in alcun binario distribuito** → serve il Cluster 8.

---

## 2. Roadmap — ordine di lavoro CONFERMATO dall'utente (2026-07-24)

> Ordine: **Cluster 6.5** (ASY-01 → SYS-01 → MEM/ARC → SEC-05) → **Cluster 7** (docs) → **fix CI** → **Cluster 8** (release coordinata).
> Ogni voce sotto è stata **ri-verificata sul codice il 2026-07-24**: nessuna è già risolta.

### Cluster 6.5 — Hardening post-audit (robustezza, memoria, architettura)

#### T6.5.1 — ASY-01 🔴 **P0 (unico P0 rimasto)** — eccezioni non gestite nei salvataggi `async void`
- **Problema:** `OnEntrySaved` / `OnEntryDeleted` (8 metodi, nei 4 List VM: `PasswordsListViewModel`, `CreditCardsListViewModel`, `IdentitiesListViewModel`, `SecureNotesListViewModel`) fanno `await _vaultState.SaveVaultAsync()` + `LogActivityAsync` **senza try/catch**. In un `async void` l'eccezione (disco pieno, DB lock, I/O error) non è catturabile → **terminazione dell'app** durante un salvataggio. Verificato: es. `PasswordsListViewModel.cs:230`.
- **Fix:** avvolgere il corpo in `try/catch`; in caso di errore mostrare messaggio chiaro via `IToastService` ("Impossibile salvare, riprova") invece del crash. I dati sono già al sicuro (SQLite è ACID: la scrittura fallita non corrompe il DB) — il problema è SOLO il crash.
- **Estensione (C2, P1, stesso intervento):** anche i `public async void SetViewModel(...)` delle View fanno `await ... LoadEntriesCommand` senza protezione → stessa classe di rischio sul load; proteggerli nello stesso branch.
- **Test:** unit dove possibile + verifica manuale simulando errore I/O.

#### T6.5.2 — SYS-01 🟠 — fail-secure su eccezione non gestita
- **Problema:** `App.xaml.cs` → `OnUnhandledException` fa `e.Handled = true` e l'app **prosegue in stato sconosciuto** (verificato: riga 217). Per un password manager è rischioso: possibili scritture incoerenti e chiavi esposte.
- **Fix (fail-secure):** 1) `Lock()` del vault (azzera il DEK via `IVaultStateService`); 2) log dell'eccezione su `startup-crash.log` (riusare il pattern `Program.WriteCrashLog` già esistente); 3) chiusura controllata (`Application.Current.Exit()`).
- **Nota:** niente scritture sul DB nel percorso di crash.

#### T6.5.3 — MEM-01 🟡 (media) — lapsed listener nelle 4 List View
- **Problema:** le View transienti (`*ListView.xaml.cs`) si iscrivono in `SetViewModel` a `vm.PropertyChanged` (+ `SaveCompleted`) e **non si disiscrivono mai** → le View scartate restano in memoria (verificato: es. `PasswordsListView.xaml.cs:38`).
- **Fix:** disiscrizione su `Unloaded` (pattern già usato altrove nel progetto, FU9). Nessun segreto coinvolto: i segreti sono già azzerati al lock (T1.6) — è un leak di alberi UI, gravità MEDIA (ricalibrata rispetto all'audit esterno che la dava ALTA).

#### T6.5.4 — MEM-02 🟡 (media) — `NavigationStack.Clear()` non rilascia i ViewModel
- **Problema:** `NavigationStack.Clear()` svuota lo stack **senza chiamare `Dispose()`** sui VM (verificato: `NavigationStack.cs:98`); `ShellViewModel` resta agganciato a eventi di singleton → non collezionabile dopo il lock.
- **Fix:** in `Clear()` (o nel percorso di lock) chiamare `Dispose()` sui VM che implementano `IDisposable` — **i VM lo implementano già tutti** (verificato), va solo invocato.

#### T6.5.5 — ARC-03 🟠 P1 — de-duplicazione dei 4 List VM in `BaseListViewModel<TEntry>`
- **Problema:** i 4 List VM condividono ~70-80% dello scheletro (~1.000 righe): `Entries`/`_allEntries`, `ApplyFilterAndSort`, `AddNew`/`EditEntry`/`CloseDetail`/`DeleteSelected`, `OnEntrySaved`/`OnEntryDeleted`, `IsDetailOpen`/`DetailViewModel`. Ogni fix va replicato 4 volte (accaduto più volte). Verificato: `BaseListViewModel` non esiste ancora.
- **Fix:** estrarre la base generica astratta sul modello di `BaseDetailViewModel<TEntry>` (già esistente); hook astratti per filtro/sort/EntityType; le sottoclassi tengono solo le specificità (es. toggle vista Carte). Stima −600/−700 righe.
- **Perché DOPO T6.5.1:** il try/catch di ASY-01 è urgente e piccolo; farlo prima significa che il refactoring lo eredita in un punto solo. Collaudo dedicato (refactoring a rischio medio).
- **ARC-01 (view caching nella shell): OPZIONALE, rimandabile** — beneficio percepibile (stato UI conservato tra navigazioni) ma non è un rischio; decidere a valle del cluster.

#### T6.5.6 — SEC-05 🟠 — Process attestation (sostituto frictionless di SEC-03)
- **Perché:** il dialog di consenso SEC-03 (interim) crea attrito a ogni sblocco; i competitor non lo richiedono. La fiducia va spostata dalla conferma dell'utente alla **verifica del processo** che parla sulla pipe.
- **Design (già deciso con l'utente, 2026-06-09):**
  - (a) **BrowserHost**: verifica che il proprio processo **padre** sia un browser reale **firmato Authenticode** (allowlist editori: Google, Microsoft, Mozilla; valutare Brave/Opera/Vivaldi) → rifiuta l'avvio da processi non riconosciuti.
  - (b) **Desktop**: verifica che il client della pipe sia il **BrowserHost.exe atteso** (via `GetNamedPipeClientProcessId` → path d'installazione; la verifica di firma propria sarà possibile solo se in futuro si adotterà il code-signing — **oggi PassKey non firma i binari**).
- **Pitfall noti:** allowlist troppo stretta rompe l'autofill sui fork Chromium; recupero PID padre; P/Invoke `WinVerifyTrust`; **testare l'autofill su OGNI browser supportato prima di fidarsi**.
- **A valle (stesso branch o immediatamente successivo):** rimuovere il dialog SEC-03 + la chiave resw orfana `IpcConsentBodyAll` (presente in tutte e 6 le lingue — verificato) + aggiornare `scripts/ipc-security-test.ps1` di conseguenza.
- **Nota architetturale:** il protocollo IPC vive in `src/PassKey.Desktop/Services/BrowserIpcService.cs`; il BrowserHost inoltra l'envelope **invariato** — ma SEC-05(a) tocca proprio il BrowserHost, quindi questo task coinvolge **entrambi** i processi.

#### T6.5.7 — DEP-01 🟠 — vulnerabilità SQLite (CVE-2025-6965 / GHSA-2m69-gcr7-jv3q) [scoperta 2026-07-24]
- **Problema:** `Microsoft.Data.Sqlite` 10.0.2 trascina `SQLitePCLRaw.lib.e_sqlite3` **2.1.11**, che imbarca un SQLite nativo < 3.50.2 con vulnerabilità di corruzione memoria di gravità ALTA (warning NU1903 in build). **Nessuna 2.1.x corretta esiste**; il fix è nella serie SQLitePCLRaw **3.0.x** (3.0.4 al 2026-07-24). Anche l'ultima Microsoft.Data.Sqlite trascina ancora la 2.1.11.
- **Rischio pratico per PassKey: BASSO** — l'exploit richiede SQL controllato dall'attaccante; PassKey usa solo query parametrizzate su DB locale, nessun input SQL esterno. Non è un'emergenza, ma va chiuso.
- **Fix candidato:** `PackageReference` diretto a `SQLitePCLRaw.bundle_e_sqlite3` 3.0.x in `PassKey.Desktop.csproj` (l'override diretto vince sul transitivo). ⚠️ Sostituisce il **motore SQLite nativo** → PR dedicata, verifica online di compatibilità con Microsoft.Data.Sqlite 10.x al momento dell'implementazione, gate con test reale su vault esistente (lettura/scrittura/backup/restore).

#### T6.5.GATE — gate di cluster
Build installer → test utente (avvio, unlock, CRUD nelle 4 sezioni con salvataggi, lock/unlock ripetuti, autofill Chrome+Firefox, harness IPC) → **VIA LIBERA** → PR.
> Suddivisione PR consigliata: T6.5.1+T6.5.2 (robustezza, piccola) · T6.5.3+T6.5.4 (memoria, piccola) · T6.5.5 (refactoring, dedicata) · T6.5.6 (SEC-05, dedicata). Gate utente almeno su: robustezza+memoria (una build) e SEC-05 (una build). Decidere caso per caso con l'utente.

### Blocco UX/Loc — notifiche e traduzioni (deciso con l'utente 2026-07-28)
> Un solo branch, una sola build, **un solo collaudo** (i tre task toccano le stesse aree).
> Collocazione proposta: **dopo il Cluster 6.5** (è rifinitura, non robustezza) — da confermare.

#### UX-02 — Sistema di notifiche a due corsie ⭐ (il pezzo grosso)
**Problema:** oggi i toast sono **incolonnati** su un'unica `InfoBar` condivisa e mostrati uno alla volta. Poiché il toast di errore non scade mai, **blocca tutti i successivi** finché l'utente non lo chiude. L'utente lo giudica inaccettabile: vuole gli avvisi persistenti sempre visibili E i messaggi informativi puntuali, senza accumulo confuso.

**Specifica approvata dall'utente:**
- **Pila di 5 slot**, ancorata in basso a destra. Due classi di messaggio:
  - **Persistenti** (errori / richiedono interazione): restano finché l'utente non li chiude. Raggruppati **in basso**.
  - **Transitori** (info / successo / avviso): scadono da soli. Galleggiano **sopra** il blocco dei persistenti.
- **Regola d'inserimento UNIFORME: il nuovo messaggio entra sempre dal basso nella propria corsia; i più vecchi salgono.** L'ordine reciproco è preservato.
  - Nuovo **persistente** → slot 1 (fondo assoluto); i persistenti più vecchi salgono; i transitori slittano su di conseguenza.
  - Nuovo **transitorio** → prima posizione sopra il blocco persistenti; i transitori più vecchi salgono.
- *Razionale della regola uniforme (scelta dell'utente):* una regola sola per entrambe le corsie, coerente col modello mentale delle notifiche di sistema (il nuovo compare dove l'occhio già guarda, i vecchi si allontanano).

```
   slot 5  │                      │
   slot 4  │ ⓘ  msg2 (a tempo)    │  ← più vecchio, salito
   slot 3  │ ⓘ  msg3 (a tempo)    │  ← transitorio più recente
   slot 2  │ ✕  msg1 (bloccato)   │  ← persistente più vecchio, salito
   slot 1  │ ✕  msg4 (bloccato)   │  ← persistente più recente: entra dal basso
```

**⏳ Regole di overflow — PROPOSTE DA ME, NON ANCORA CONFERMATE dall'utente:**
- Slot esauriti + arriva un **persistente** → si accoda e compare appena se ne libera uno (**un errore non va mai perso**).
- Slot esauriti + arriva un **transitorio** → chiude in anticipo il transitorio **più vecchio** (accodarlo tradirebbe il requisito "compaia al momento giusto").
- **Riserva**: i persistenti occupano al massimo **4** slot; 1 slot resta **sempre** disponibile per la corsia transitoria (altrimenti 5 errori riprodurrebbero il blocco che questa modifica elimina).

**Note d'implementazione:** sostituire l'`InfoBar` singola in `MainWindow.xaml` con un contenitore di N barre; `ToastService` perde la pompa seriale e gestisce timer per-messaggio. Attenzione a: annunci per screen reader su più live region, movimento verticale alla comparsa di un persistente, altezza totale della pila su finestre piccole.

#### UX-03 — Allineare tutti i toast alla mappatura approvata
Mappatura **approvata dall'utente 2026-07-28** (il colore descrive **l'esito**, non la natura dell'azione; il livello determina anche la durata):

| Evento | Livello | Durata |
|---|---|---|
| Copia negli appunti | ⓘ Informativo | 3s |
| Salvataggio riuscito | ✓ Successo | 5s |
| **Eliminazione riuscita** | **ⓘ Informativo** | 3s |
| Avvisi (auto-lock…) | ⚠ Avviso | 5s |
| Salvataggio/eliminazione fallita | ✕ Errore | persistente |

- **UX-01 (già fatto** nel branch `fix/2.0/asy01-save-errors`, commit `74fb1cc`): applica la mappatura ai soli toast di **eliminazione** (8 punti). **Resta da fare**: verificare TUTTI gli altri toast dell'app e allinearli.
- *Decisioni collegate:* **"Annulla"/undo sul toast di eliminazione → SCARTATO** dall'utente. **Scollegare durata da livello → decaduto** (serviva solo per l'undo); da riaprire solo se in collaudo i 3s risultassero troppo brevi per leggere "Elemento eliminato".

#### LOC-01 — Verifica completa delle traduzioni (scelta utente: **completa**, non minima)
- **Refusi noti** nella chiave `ToastDeleted`: fr-FR `Element supprime` → `Élément supprimé`; de-DE `Element geloscht` → `Element gelöscht`.
- **Scope deciso:** non correggere solo questi due, ma **passare in rassegna tutti e 6 i `.resw`** cercando altri casi di diacritici mancanti.
- **Ipotesi sulla causa (da verificare):** gli script `add-resw-keys.ps1` / `add-login-resw-keys.ps1` (untracked, nella root) potrebbero aver inserito chiavi in blocco senza caratteri accentati → il difetto potrebbe ripetersi in altre chiavi e in lingue che l'utente non usa.

### Cluster 7 — Documentazione utente
- **T7.1** — user-guide **TOTP** (setup, import QR/URI/manuale, uso dei codici). *Verificato: oggi la user-guide non menziona mai TOTP.*
- **T7.2** — user-guide **Watchtower/HIBP** (cos'è, k-anonymity, opt-in, lettura dei risultati). *Verificato: mai menzionato.*
- File: `docs/user-guide/` (aggiungere `09-totp.md`, `10-watchtower.md` o integrare nelle pagine esistenti — decidere con l'utente). Lingua: coerente con le guide esistenti.
- CHANGELOG sezione Security: **già fatto** in #52 — nessuna azione.
- Gate: solo revisione utente (docs-only, niente installer).

### Cluster 8 — Release coordinata Desktop + estensioni

#### T8.1 — Fix CI `release.yml` 🔴 (bloccante per qualunque tag)
- **Problema (verificato 2026-07-24):** `release.yml` builda l'installer con `scripts/build-installer.ps1`, che richiede `Installer/WindowsAppRuntimeInstall-x64.exe` — file **gitignorato e mai scaricato in CI** → ogni tag fallisce (la v2.0.0 fu pubblicata a mano). Lo script `Installer/Download-Runtime.ps1` esiste già ma **la CI non lo chiama**.
- **Fix:** aggiungere in `release.yml` uno step `.\Installer\Download-Runtime.ps1` prima del build installer. Verificare anche versione runtime allineata al WindowsAppSDK del progetto.

#### T8.2 — Ricreare gli script di packaging estensioni ⚠️ (PERSI)
- **Fatto verificato 2026-07-24:** `make-xpi.ps1` e `make-chrome-zip.ps1` **non esistono più** da nessuna parte nel repo (erano probabilmente file non versionati, persi). Vanno **ricreati e versionati in `scripts/`**:
  - `make-chrome-zip.ps1`: ZIP della cartella `extensions/chrome` **strippando la chiave `key` dal manifest** (requisito Web Store).
  - `make-xpi.ps1`: XPI da `extensions/firefox`.
  - Miglioria rispetto ai vecchi: **leggere la versione dal `manifest.json`** per il nome file (i vecchi hardcodavano "1.0.0").
- ID store (invarianti): Chrome `jadfnbfppmcpbfiickiolonfldkphmfb` · Firefox `{3E08FACC-D43B-4B20-89E7-7888F6082E9D}`. Il guard in `build-installer.ps1` blocca l'ID di sviluppo.

#### T8.3 — Version bump coordinato
- Desktop → v2.0.1 (o 2.1.0 se SEC-05 è considerata feature — decidere con l'utente); estensioni → 1.0.2 se toccate da SEC-05/fix.
- CHANGELOG aggiornato.

#### T8.4 — Tag + GitHub Release
- Tag `v*.*.*` su `main` → CI (fixata in T8.1) produce installer + portable. Verificare artefatti.

#### T8.5 — Re-upload estensioni sugli store **in un'unica volta**
- Chrome Web Store + Firefox AMO (per questo il lavoro estensioni è stato accumulato: UNA sola revisione store, UN solo collaudo 6 lingue).

#### T8.GATE — gate finale
Installer dalla release CI (non build locale) testato dall'utente prima di pubblicare gli asset/estensioni.

---

## 3. Backlog P2 (non pianificato, da ripescare solo su richiesta)
- Logging strutturato (`ILogService` file rotante) al posto dei 7 `Debug.WriteLine` — lega a X4 (feedback offline HIBP).
- X1: riepilogo esplicito a fine import ("N importati, M saltati, K duplicati").
- Rimozione codice orfano: `vm.PanelTitle` nei Detail VM; `AuditLoadingRing` se non riusato.
- Messaggi d'import in `.resw` (oggi stringhe italiane hardcoded).
- U1/U2/U3 (densità liste, indicatore stato vault, coerenza icone Dashboard/Verifica).
- ARC-01 view caching · ARC-02 allineamento navigazione a `INavigationStack`.
- Migrazione `MarkdownTextBlock` → CommunityToolkit Labs **solo quando** uscirà una release stabile (oggi 7.1.2 legacy funzionante).
- CI: valutare `dotnet test` su ogni PR è **già coperto** dal check `test` richiesto su `main` (`ci.yml`).

## 4. Promemoria e decisioni aperte
- 🔔 **pm-export**: gli export reali in chiaro dei password manager (Bitwarden/1Password/KeePass) vanno **eliminati definitivamente a fine ciclo 2.x.x** (servono ancora per collaudare gli importer). Ricordarlo periodicamente all'utente — sua richiesta esplicita del 2026-05-30.
- Worktree obsoleto `cool-wilson-213cae` ancora su disco: eliminabile quando l'utente vuole.
- File scratch untracked nella root del repo (checklist storiche, script di supporto, `analisi/`): decidere a fine ciclo cosa versionare (es. `analisi/` come documentazione) e cosa eliminare.
- Naming file user-guide Cluster 7 (vedi T7).
- Versione target del prossimo rilascio (2.0.1 vs 2.1.0, vedi T8.3).

## 5. Changelog del piano
- **2026-07-24 · v2.0**: ricostruzione completa dopo la perdita del piano originale; roadmap riverificata voce per voce sul codice; aggiunto T8.2 (script packaging persi, da ricreare); ordine di lavoro confermato dall'utente.
