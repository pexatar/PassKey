# Rifondazione dello strato elenco/dettaglio — passo 1: fondamenta + sezione pilota

> **Branch:** `refactor/2.0/ui-rifondazione` (da `fix/2.0/asy01-save-errors`)
> **Stato:** ⛔ **NON COLLAUDATO.** Nessuna PR, nessun merge, nessun tag.
> **Riferimenti:** `ARCH-UI-spec.md` (locale, non nel repo) · `docs/HANDOFF-CLOUD.md` §6 · `docs/PLAN.md` T6.5.5

---

## 1. Che cosa è stato fatto

Le **fondamenta** previste dal §3 di `ARCH-UI-spec.md`, e **una sola sezione** portata sopra
di esse: **Note Sicure**. Le altre tre sezioni (Password, Carte, Identità) non sono state
toccate: continuano a funzionare com'erano, con le loro compensazioni.

È deliberato. Se il progetto è sbagliato, si butta via una sezione, non quattro.

---

## 2. Analisi d'impatto R0

**Devo fare A.** Dare alle righe dell'elenco oggetti che notificano i propri cambiamenti, una
classe base condivisa per gli elenchi, e un ciclo di vita esplicito del pannello di dettaglio.

**A genera problemi? Sì, due scelte potevano generarne.**

### 2.1 Dove mettere la capacità di notificare

La via ovvia — aggiungerla ai 4 modelli di `PassKey.Core` — è stata **scartata**:

| Motivo | Verifica |
|---|---|
| Il perimetro esclude `Core` | `ARCH-UI-spec.md` §1.2 e §3, handoff §6 |
| `Core` è compilato AOT e "trimmable" | `PublishAot`, `IsTrimmable` in `PassKey.Core.csproj` |
| `Core` è il contratto di serializzazione del caveau | `VaultJsonContext` genera il codice di serializzazione |
| Due audit indipendenti lo giudicano solido | `ARCH-UI-spec.md` §1.2 |
| Le righe hanno bisogno di valori che **non** appartengono al modello | anteprima a 80 caratteri, nome categoria tradotto, data relativa, intestazione di sezione |

→ **Adottato:** un oggetto-riga nello strato desktop che avvolge il modello.
**`PassKey.Core`: zero righe modificate** (verificabile con `git diff main...HEAD -- src/PassKey.Core`).

### 2.2 La classe base tocca tutte e quattro le sezioni?

Se imposta subito ai 4 elenchi, starei modificando quattro sezioni dichiarando di modificarne
una. → **Adottato:** la base è **aggiunta**, non imposta. Solo Note la usa.

### 2.3 Come evito che il problema si ripresenti

Le compensazioni non sono state aggirate ma **rimosse**. La loro rimozione è la prova che la
fondazione regge: se una fosse dovuta restare, vorrebbe dire che non regge.

### 2.4 Impatto sull'intera struttura (misurato)

| Area | Impatto | Verifica |
|---|---|---|
| Dashboard, Verifica, Generatore, Impostazioni | **Nessuno** | leggono `vault.SecureNotes` direttamente, non passano dal ViewModel dell'elenco |
| Importatori, backup, canale estensioni | **Nessuno** | vivono in `Core` / `BrowserHost` |
| `ShellView` (navigazione, Ctrl+N) | **Nessuno** | `SetViewModel` e `InvokeAddNew` hanno la stessa firma |
| Password, Carte, Identità | **Nessuno** | non toccate in questo passo |
| Traduzioni | **Nessuno** | zero chiavi nuove: le 24 chiavi usate esistevano già in tutte e 6 le lingue |

---

## 3. Le compensazioni rimosse da Note Sicure

| Compensazione | Difetto che causava | Stato |
|---|---|---|
| `ContainerContentChanging` — righe disegnate a mano percorrendo l'albero visuale | FUN-02, elenco con dati vecchi | ✅ rimossa |
| `ListRevision` + `RefreshListContainers` — rimedio ponte | — | ✅ rimossi, non serve più nulla da "ponteggiare" |
| `_updatingFromVm` — semaforo di sincronizzazione campo↔dato | UI-01, cursore che salta | ✅ rimosso |
| `DetailViewModel = null` e poi `= _detailVm` per forzare una notifica | pannello che mostra la voce sbagliata | ✅ rimosso: ogni sessione ha un proprio ViewModel |
| `Visibility`, stile dei pulsanti e testi scritti da code-behind | stati incoerenti | ✅ sostituiti da collegamenti dichiarativi |
| Iscrizione agli eventi senza disiscrizione | MEM-01, pannelli fantasma | ✅ simmetria garantita dalla classe base |

---

## 4. Numeri reali (misurati, non stimati)

**Perimetro della sezione — 7 file:**

| File | Prima | Dopo |
|---|---:|---:|
| `SecureNotesListViewModel.cs` | 376 | 146 |
| `SecureNoteDetailViewModel.cs` | 211 | 283 |
| `Base/BaseDetailViewModel.cs` | 228 | 266 |
| `SecureNotesListView.xaml` | 227 | 240 |
| `SecureNotesListView.xaml.cs` | **414** | **229** |
| `SecureNoteDetailView.xaml` | 194 | 221 |
| `SecureNoteDetailView.xaml.cs` | **275** | **104** |
| **Totale** | **1.925** | **1.489** |

**Il numero che conta: il code-behind delle due viste passa da 689 a 333 righe (−52%).**
È lì che vivevano le compensazioni. I ViewModel e lo XAML crescono perché la logica si è
spostata dove è verificabile: proprietà osservabili e collegamenti dichiarativi.

**Fondamenta aggiunte: 893 righe**, oggi usate da una sola sezione. Sono il capitale che le
altre tre useranno senza riscriverlo — è lì che l'investimento rientra, non adesso.

---

## 5. ⚠️ Lista di collaudo — Note Sicure

Da provare **solo sulla sezione Note Sicure**. Tutto ciò che segue funzionava prima e deve
continuare a funzionare.

### L'elenco

- [ ] Le note compaiono, con le **fissate in cima** e poi in ordine di ultima modifica
- [ ] Intestazione **"Fissate"** sopra la prima nota fissata, **"Note"** sopra la prima non fissata che segue delle fissate
- [ ] Ogni card mostra: **barra colorata** della categoria, **puntina** se fissata, **titolo**, **data relativa** ("2 ore fa"), **anteprima** del testo, **nome categoria**
- [ ] **Filtro categoria** (icona a imbuto): elenco con pallini colorati, voce "Tutte le categorie", **puntino badge** sull'icona quando un filtro è attivo
- [ ] **Ricerca**: filtra su titolo *e* contenuto
- [ ] **"Nessuna nota"** quando il caveau non ne ha; **"Nessun risultato"** quando i filtri non trovano nulla
- [ ] Tasti: **F2** apre la nota selezionata · **Canc** elimina (con conferma) · **Esc** chiude l'editor · **Ctrl+N** nuova nota

### L'editor

- [ ] **Titolo vuoto** → compare "Campo obbligatorio" e **Salva è spento**
- [ ] **Pallino** accanto a "Titolo" quando ci sono modifiche non salvate
- [ ] Menu **Categoria** con i pallini colorati
- [ ] Contatore **"N car · M parole"** che si aggiorna mentre si scrive
- [ ] **Modifica / Anteprima**: il pulsante attivo è evidenziato, l'anteprima rende il Markdown
- [ ] **Puntina**: salva subito senza passare da Salva, e la nota si sposta in cima
- [ ] **Elimina**: visibile solo su note esistenti, chiede conferma
- [ ] **Annulla** chiude senza salvare
- [ ] **Salva**: rotella durante il salvataggio, poi avviso di conferma

### ⭐ I punti per cui esiste questo lavoro

- [ ] **Modifica il titolo di una nota e salva → l'elenco mostra SUBITO il nuovo titolo.** Idem cambiando categoria (cambia il colore della barra), contenuto (cambia l'anteprima) e la data
- [ ] **Scrivi nel titolo e nel corpo: il cursore non salta**, neanche modificando a metà testo o incollando
- [ ] **Apri una nota, chiudila, aprine un'altra**: il pannello mostra sempre quella giusta
- [ ] **Vai su un'altra sezione e torna su Note** con l'editor aperto: nulla resta bloccato
- [ ] **Blocca e sblocca il caveau**: l'elenco si svuota e si ricarica correttamente

### Se qualcosa non va

1. Impostazioni → Diagnostica → attiva **"Log dettagliati"**
2. Ripeti l'operazione
3. Impostazioni → Diagnostica → **"Apri cartella"** e allega il file

Righe utili: `List | List rebuilt`, `Detail | Panel opened for edit`,
`Command | Save refused`, `Persist | Vault saved` / `Vault save FAILED`.

---

## 6. ⚠️ Da rimuovere prima di qualunque merge

`.github/workflows/ci.yml` contiene **due modifiche temporanee**, entrambe segnate da un
commento `TEMPORARY`:

1. il branch `refactor/2.0/ui-rifondazione` aggiunto ai trigger di `push`;
2. `if: always()` sul passo `Build Desktop`.

Servono perché la sessione cloud gira su Linux e non può compilare WinUI: è l'unico modo di
avere una compilazione Windows vera ad ogni push senza aprire una PR. **Vanno tolte entrambe.**

---

## 7. Cosa NON è stato fatto

- **Password, Carte, Identità**: invariate. Si procede solo dopo il via libera sul pilota.
- **Perimetro invariato**: Dashboard, Impostazioni, Generatore, Verifica e tutto `PassKey.Core`.
- **Comportamento non verificato**: la CI dimostra che il codice compila e che i 222 test
  restano verdi, **non** che la sezione si comporti come deve. I 222 test non referenziano
  nemmeno il progetto `PassKey.Desktop`: sullo strato riscritto la copertura automatica è
  **zero**. L'unica verifica possibile è il collaudo manuale del §5.
