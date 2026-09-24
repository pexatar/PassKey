# Handoff — ripresa del lavoro su PassKey 2.x

> **Documento autosufficiente.** Scritto per una sessione che non ha mai visto le conversazioni
> precedenti. Leggilo **per intero prima di toccare qualsiasi cosa**.
>
> **Data:** 2026-09-24 · **Branch di riferimento:** `fix/2.0/asy01-save-errors`

---

## 0. La cosa più importante da sapere subito

⚠️ **Il codice presente su questo branch NON ha superato il collaudo dell'utente.**

Il progetto ha una **regola di processo inviolabile** (§5): nessun merge su `main` e nessun
rilascio senza che l'utente abbia installato e provato manualmente una build e abbia dato un
**"VIA LIBERA" esplicito**. I 5 commit su questo branch sono stati pushati **solo** per
trasferire il lavoro a una sessione cloud, con l'accordo esplicito dell'utente e a tre
condizioni: **nessuna PR aperta**, gate pienamente valido prima di qualunque merge, e questa
avvertenza scritta qui.

**Non aprire PR. Non mergiare su `main`. Non taggare rilasci.**

---

## 1. Che cos'è PassKey

Gestore di password per Windows (WinUI 3, .NET 10, self-contained) con estensioni per Chrome e
Firefox che comunicano con l'app tramite named pipe locale. Il caveau è cifrato e salvato in
SQLite. È la **quarta riscrittura** del progetto: le tre precedenti sono fallite per accumulo
di correzioni tampone fino all'ingestibilità. Questo contesto spiega le regole rigide che
seguono e la scelta di rifondare invece di rattoppare.

Struttura del codice:
- `src/PassKey.Core` — crittografia, modelli, importatori, backup. **Giudicato solido da due
  audit indipendenti. Fuori dal perimetro di lavoro attuale.**
- `src/PassKey.Desktop` — applicazione WinUI 3 (viste, ViewModel, servizi)
- `src/PassKey.BrowserHost` — ponte per la messaggistica nativa del browser
- `src/PassKey.Tests` — **222 test, devono restare tutti verdi ad ogni passo**
- `extensions/chrome`, `extensions/firefox` — estensioni MV3
- `docs/PLAN.md` — piano di sviluppo del ciclo 2.x *(vive sul branch `fix/2.0/plan-doc`,
  PR #53 aperta; se non lo trovi qui è per quello)*

---

## 2. Stato: cosa c'è su questo branch

Cinque commit, non collaudati, in ordine cronologico:

| Commit | Contenuto |
|---|---|
| `1d5cdbb` | **ASY-01** — protezione dei percorsi di salvataggio: un errore di scrittura non termina più il processo; l'errore viene mostrato all'utente |
| `7822843` | **DLG-01** — tutte le finestre di dialogo passano dalla coda serializzata; WinUI ne ammette una sola per volta e le eccedenze causavano la chiusura dell'app |
| `74fb1cc` | **UX-01** — le eliminazioni riuscite usano un avviso neutro anziché "successo" |
| `c4f9ab5` | **LOG-01** (sistema di log) + correzioni strutturali — vedi §3 |
| `ee1ca68` | Colore della carta nell'elenco, impaginazione dei pulsanti 2FA, accenti mancanti in 4 lingue, fail-secure sulle eccezioni non gestite |

Build pulita e **222/222 test verdi** dopo ciascuno.

---

## 3. La causa radice individuata — è il cuore di tutto

Durante il collaudo sono emersi otto difetti apparentemente scorrelati (elenchi che mostravano
dati vecchi, pulsanti attivi che non facevano nulla, cursore che saltava nei campi formattati,
pannelli che sembravano bloccati). **Non sono otto difetti: sono otto sintomi di una sola
decisione fondativa mancata.**

**I quattro modelli del caveau — `PasswordEntry`, `CreditCardEntry`, `IdentityEntry`,
`SecureNoteEntry` — non implementano `INotifyPropertyChanged`.**

Conseguenza: il collegamento dati non può funzionare. Quando una voce viene modificata
l'interfaccia non ha modo di accorgersene. Da lì è nata una catena di compensazioni scritte a
mano, ciascuna sensata da sola, tutte insieme una struttura che si regge sulle pezze:

| Compensazione presente nel codice | Sintomo che ne è derivato |
|---|---|
| Righe di elenco popolate a mano (`ContainerContentChanging`) | L'elenco mostra valori vecchi dopo un salvataggio |
| Rimedio `RefreshListContainers()` presente **solo in Identità** | Il difetto spariva in 1 sezione su 4 — firma della copia-incolla, non di un progetto |
| Sincronizzazione campo↔dato con semaforo `_updatingFromVm` | Cursore che salta nel numero carta |
| `IsEnabled` dei pulsanti scritto da 3 punti in conflitto | Pulsante "Salva" acceso che non eseguiva nulla |
| Comando che esce in silenzio (`if (!CanSave) return;`) | Nessuna spiegazione all'utente |
| Un solo ViewModel di dettaglio riusato per tutte le voci | Comportamenti divergenti fra sezioni |
| Viste ricreate ad ogni apertura che si iscrivono agli eventi e **non si disiscrivono mai** | Accumulo di pannelli non più raggiungibili |
| Quattro ViewModel di elenco copiati (~1.000 righe duplicate) | Ogni correzione va replicata 4 volte, e non lo è stata |

**Decisione presa con l'utente: rifondare lo strato, non tamponarlo.** Il progetto completo è
in `ARCH-UI-spec.md` *(vedi §8: non è nel repository)*.

---

## 4. Cosa è già stato corretto, e come

Le correzioni sono **strutturali**, non tampone — salvo una, dichiarata:

- **Pulsanti legati ai comandi.** In tutti e 4 i pannelli di dettaglio il pulsante Salva è
  agganciato a `SaveCommand`, con `[NotifyCanExecuteChangedFor]` su `CanSave`. **Tutte** le
  assegnazioni manuali di `SaveButton.IsEnabled` sono state rimosse. Un comando che non può
  essere eseguito non può più sembrare attivo.
- **Validazione delle carte**: non rifiuta più il salvataggio di una carta con scadenza
  passata. Bloccava il caso d'uso più frequente ("la carta è scaduta, l'ho rinnovata, aggiorno
  la data"), e lo faceva in silenzio. L'elenco degli anni ora contiene sempre l'anno della
  carta in modifica.
- **Cursore nei campi formattati**: tracciato per **numero di cifre**, non per indice di
  carattere. Regge anche modifiche a metà stringa, incolla e cancellazioni.
- **Fail-secure**: un'eccezione non gestita azzera la chiave di cifratura prima di mostrare la
  schermata d'errore. Un processo in stato ignoto non è abbastanza affidabile da tenere in
  memoria un caveau sbloccato.
- ⚠️ **Rimedio ponte dichiarato**: Note e Carte rigenerano le righe dopo un salvataggio
  (proprietà `ListRevision`). **È una compensazione, non una soluzione**: sparisce quando le
  righe leggeranno oggetti che notificano i propri cambiamenti.

### LOG-01 — il sistema di log (leggere prima di diagnosticare qualsiasi cosa)

Introdotto perché ogni diagnosi precedente era stata fatta interrogando l'utente invece di
leggere un file. `ILogService` / `LogService`:

- **Un file per sessione**: `passkey_<data>_<ora>_<pid>.log`
- **Formato a colonne fisse**: `data | LIVELLO | AREA | messaggio | dettagli`
- **Posizione**: cartella `logs` accanto all'eseguibile **se scrivibile**, altrimenti
  `%LocalAppData%\PassKey\logs`. La scrivibilità è **provata con una scrittura reale**:
  l'app si installa in una cartella di sistema dove un utente normale non può scrivere, ed è
  questo che faceva fallire in silenzio i tentativi precedenti.
- **Livello base sempre attivo** (Error/Warn/Info) + **verboso** attivabile da
  Impostazioni → Diagnostica, con effetto immediato. 20 sessioni conservate, 10 MB per file.
- Scrittura su coda in background: non rallenta l'interfaccia e **non lancia mai eccezioni**.

🔒 **Vincolo assoluto quando si aggiungono righe di log**: mai password, PAN, CVV, PIN, seed
TOTP, contenuto delle note, dati delle identità o materiale crittografico. Solo
identificativi, tipi, esiti, durate e conteggi. I log sono file in chiaro pensati per essere
allegati a una segnalazione.

Righe già presenti che rispondono da sole alle domande più frequenti:
- `Detail | Panel opened for edit | type=… entryId=…` → **quale** voce è realmente in modifica
- `Command | Save refused: … | motivo=…` → perché un comando non ha prodotto effetti
- `Persist | Vault saved | bytes=… ms=…` / `Vault save FAILED` → se la scrittura è arrivata al disco

---

## 5. Regole di processo — inviolabili

1. **Gate di collaudo utente.** Prima di *qualunque* merge su `main` o rilascio: build →
   l'utente installa e prova manualmente → **"VIA LIBERA" esplicito in chat**. Un "procedi
   pure" generico **non** vale come via libera.
2. **`main` è protetto.** Solo PR con check verde e squash-merge. Mai push diretto.
3. **Durante un collaudo si annota, non si implementa.** Ogni osservazione che emerge mentre
   l'utente sta provando una build va in una lista "da decidere". Nessuna modifica al codice e
   nessuna ricompilazione senza un "procedi" esplicito su quel punto: cambiare il codice a
   collaudo in corso invalida la build sotto test.
4. **R0 — analisi d'impatto obbligatoria prima di ogni modifica**, per iscritto:
   *devo fare A → A genera problemi? → se sì, quali soluzioni e come evito che si ripresentino
   altrove? → come impatta sull'intera struttura?*
   Tutti i difetti trovati finora nascono da modifiche corrette *in locale* e mai valutate
   *sull'insieme*.
5. **Dubitare delle fonti locali.** Memoria, documenti e riassunti possono essere obsoleti:
   verificare su codice e git. Le ricerche online sono sempre autorizzate e vanno fatte
   multiple e in parallelo quando servono.
6. **L'utente non è uno sviluppatore.** Spiegazioni chiare e schematiche in italiano; non
   chiedergli mai di lanciare script, build o comandi — li esegue l'assistente. Le build si
   producono con `scripts/build-installer.ps1`.
7. **Misurare prima di descrivere.** Quando si parla dell'ampiezza di un intervento, dare
   numeri verificati. La frase *"è solo una piccola porzione, è routine"* ha preceduto il
   fallimento delle versioni precedenti.

---

## 6. Prossimo passo atomico

**Eseguire la rifondazione dello strato elenco/dettaglio, partendo dalle fondamenta e poi da
una sola sezione pilota: Note Sicure.**

Ordine previsto:
1. **Fondamenta**: oggetti di riga che notificano i propri cambiamenti, classe base condivisa
   per gli elenchi, ciclo di vita esplicito del pannello di dettaglio (una istanza per
   sessione di modifica), aggancio/sgancio simmetrico degli eventi.
2. **Sezione pilota: Note Sicure** (la più semplice) riscritta sulle nuove fondamenta.
3. **Fermarsi e far collaudare la sola sezione pilota.** È deliberato: se il progetto è
   sbagliato lo si scopre dopo una sezione, non dopo quattro. L'utente teme — a ragione, per
   esperienza diretta — di investire giorni e sentirsi dire "meglio rifare da capo".
4. Solo dopo: Carte (la più complessa), Password, Identità.

**Perimetro, da non superare:** solo elenco e dettaglio delle 4 sezioni del caveau.
**Fuori perimetro:** Dashboard, Impostazioni, Generatore, Verifica, e tutto `PassKey.Core`.

Le 9 regole di progetto che governano la riscrittura sono in `ARCH-UI-spec.md` (§8). In
sintesi: nessuna sincronizzazione manuale campo↔dato, nessun `IsEnabled` da code-behind,
nessun comando che esce in silenzio, un ViewModel di dettaglio per sessione di modifica,
nessuna sottoscrizione manuale vista→ViewModel, nessuna riga disegnata a mano, una sola
implementazione condivisa, ogni transizione di stato nel log, nessun `async void` senza
`try/catch`.

---

## 7. Trappole note del progetto

Verificate sul campo, costano ore se riscoperte:

- **`dotnet build` richiede `-p:Platform=x64`** (pubblicazione self-contained del Windows App SDK).
- **Il compilatore XAML fallisce in modo silenzioso**: se `dotnet build` termina con MSB3073
  senza spiegazioni, ricompilare con MSBuild full-framework per vedere gli errori veri.
- **`[ObservableProperty]` su campi non è compatibile con AOT** in questo contesto: usare la
  forma `partial property` (avviso MVVMTK0045).
- **`x:Bind` è OneTime per impostazione predefinita**: dichiarare sempre `Mode=OneWay` o
  `Mode=TwoWay` esplicitamente. È una delle ragioni per cui si era finiti ad aggiornare
  l'interfaccia a mano.
- **Un comando non si accorge da solo** che la sua condizione di esecuzione è cambiata: serve
  `[NotifyCanExecuteChangedFor]`, altrimenti il pulsante non si aggiorna.
- **`ObservableCollection` non propaga i cambiamenti *interni* agli elementi**: la riga si
  aggiorna solo se l'oggetto stesso notifica i propri cambiamenti.
- **Mai toccare oggetti dell'interfaccia fuori dal thread UI** in una continuazione: causa un
  blocco totale difficile da diagnosticare (già accaduto, con il server IPC bloccato).
- **I brush di sistema letti da code-behind non seguono il tema**: usare i brush personalizzati
  del progetto o `{ThemeResource}` nel XAML.
- **Modifiche alle stringhe vanno replicate in tutte e 6 le lingue** (it, en, fr, de, es, pt) e
  l'XML va rivalidato.
- **Il cambio lingua richiede il riavvio del processo** e va applicato nel costruttore
  dell'applicazione prima dell'inizializzazione.

---

## 8. Cosa NON troverai nel repository

Alcuni documenti di lavoro vivono **solo sulla macchina dell'utente**, per scelta: contengono
dettagli non adatti a un repository pubblico. Se ti servono, **chiedili all'utente**.

| Documento | Contenuto |
|---|---|
| `ARCH-UI-spec.md` | Progetto completo della rifondazione: le 9 regole con la motivazione di ciascuna, struttura di destinazione, ordine di esecuzione, rischi e contromisure |
| `STATO-LAVORI.md` | Stato voce per voce di tutto ciò che è fatto e ciò che manca |
| `LOG-01-spec.md` | Specifica completa del sistema di log |
| Registri di collaudo | Difetti rilevati dall'utente, con diagnosi e rimedio proposto |

**Voci ancora aperte, citate per sigla** (il dettaglio è nei documenti locali): UI-03
(impaginazione di un selettore), UX-02 e UX-03 (sistema di notifiche e relativa mappatura),
LOC-01 (revisione integrale delle traduzioni: 44 stringhe corrette, ne restano molte da
verificare), A7 (registrazione di attività ridondanti), DEP-01 (valutazione di un
aggiornamento di dipendenza, vedi avviso di build), SEC-05 (irrobustimento pianificato del
canale di comunicazione con le estensioni).

---

## 9. Comandi utili

```
dotnet build src/PassKey.Desktop/PassKey.Desktop.csproj -p:Platform=x64
dotnet test src/PassKey.Tests/PassKey.Tests.csproj      # baseline: 222 verdi
scripts/build-installer.ps1                              # produce installer + portable
```

---

## 10. Riassunto in cinque righe

1. Questo branch contiene lavoro reale ma **non collaudato**: non mergiarlo, non rilasciarlo.
2. La causa di tutti i difetti recenti è **una sola**: i modelli non notificano i propri cambiamenti.
3. La decisione presa è **rifondare** lo strato elenco/dettaglio, non tamponarlo.
4. Si parte dalle fondamenta e da **una sola sezione pilota**, poi ci si ferma e si fa collaudare.
5. Prima di diagnosticare qualunque cosa: **attivare i log e leggerli**, invece di interrogare l'utente.
