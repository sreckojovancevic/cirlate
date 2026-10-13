# CIRLAT — Aplikacija za normalizaciju naziva fajlova

## 1. Svrha aplikacije

CIRLAT je Windows desktop aplikacija za grupno preimenovanje fajlova i direktorijuma.

Osnovna funkcija je automatska konverzija naziva iz srpske ćirilice ili latinice u latinicu bez dijakritičkih znakova.

**Obavezno pravilo:** nijedan konačni naziv ne sme sadržati srpske dijakritičke znakove.

Aplikacija menja isključivo nazive, nikada sadržaj fajlova.

## 2. Pravila normalizacije

Obrada se izvršava sledećim redosledom:

0. Unicode NFC normalizacija (nazivi sa Mac-a i iz nekih web formi imaju `č` zapisan kao `c` + kombinujuća kvačica; bez ovog koraka dijakritik bi ostao).
1. Konverzija ćirilice u srpsku latinicu.
2. Uklanjanje dijakritičkih znakova.
3. Normalizacija slova `đ` u `dj`.
4. Provera konačnog naziva i mogućih konflikata.
5. Preimenovanje nakon potvrde korisnika.

### 2.1. Zamena znakova

| Ulazni znakovi | Izlaz |
|---|---|
| č, Č, ć, Ć | c, C |
| š, Š | s, S |
| ž, Ž | z, Z |
| đ, Đ | dj, Dj |
| љ, Љ | lj, Lj |
| њ, Њ | nj, Nj |
| џ, Џ | dz, Dz |
| Sva ostala ćirilična slova | Odgovarajuća latinična slova |

Za slovo `џ` primenjuje se postupak `џ → dž → dz`, jer konačni naziv ne sme sadržati dijakritike.

**Velika slova kod digrafa:** ako je slovo koje sledi veliko (ili, na kraju reči, slovo ispred), oba izlazna slova su velika: `ЉУБАВ` → `LJUBAV`, `ЂОРЂЕ` → `DJORDJE`, `ПЕЂ` → `PEDJ`. U ostalim slučajevima samo prvo: `Љубав` → `Ljubav`, samostalno `Ђ.` (inicijal) → `Dj.`

Obrađuju se i latinični digrafi zapisani kao jedan Unicode znak (`ǅ`, `ǈ`, `ǋ` i njihove varijante).

### 2.2. Primeri

| Original | Rezultat |
|---|---|
| `Извештај.docx` | `Izvestaj.docx` |
| `Ђорђе.docx` | `Djordje.docx` |
| `Чачак.pdf` | `Cacak.pdf` |
| `Љубав.txt` | `Ljubav.txt` |
| `Његош.pdf` | `Njegos.pdf` |
| `Џемпер.jpg` | `Dzemper.jpg` |
| `Čačak_Šabac.docx` | `Cacak_Sabac.docx` |
| `Đorđe_Ćirić.pdf` | `Djordje_Ciric.pdf` |

## 3. Ekstenzije fajlova

- Ekstenzija se čuva u izvornom obliku.
- Podrazumevano se obrađuje samo naziv fajla, bez ekstenzije.
- Ako ekstenzija sadrži nedozvoljene ili dijakritičke znakove, aplikacija je neće automatski menjati u osnovnoj verziji, već će upozoriti korisnika da strogi ASCII uslov nije ispunjen.
- Višestruke ekstenzije, kao što je `.tar.gz`, moraju ostati očuvane.

## 4. Izbor direktorijuma

Aplikacija omogućava:

- izbor jednog ili više direktorijuma;
- obradu fajlova i direktorijuma;
- opcionalnu obradu poddirektorijuma;
- prikaz ukupnog broja pronađenih stavki;
- isključivanje pojedinačnih stavki iz obrade.

### 4.1. Zaštićene lokacije (obavezno)

Nad zaštićenim lokacijama CIRLAT ne radi **ništa**: ne mogu se izabrati, ne obilaze se i ništa ispod njih se ne preimenuje. Nijedna putanja nije zakucana, jer sistem ne mora biti na `C:`, a profili ni Program Files ne moraju biti na sistemskom disku.

1. **Stvarne lokacije sistema, pitane od Windowsa:** Windows, System, Program Files (oba), Common Files, ProgramData; folder sa profilima (`ProfileList\ProfilesDirectory`, može biti i `D:\Users`), `Default`, `Public` i profil svakog korisnika na mašini (`ProfileImagePath`).
2. **Ista imena u korenu svakog diska i share-a**, bez obzira na to koji je sistemski: `Windows`, `Windows.old`, `Program Files`, `Program Files (x86)`, `ProgramData`, `Users`, `$Recycle.Bin`, `System Volume Information`, `Recovery`, `PerfLogs`, `Boot`, `$WinREAgent`, `Config.Msi` i slično. Pokriva dual boot, stari sistemski disk prikačen kao drugo slovo i ručno napravljen `D:\Program Files`.
3. **Folderi instaliranih programa** gde god da su: `InstallLocation` iz Uninstall ključeva (HKLM 64/32, HKCU).

Dodatno:

- **Koreni diskova i share-ova** se nikad ne obrađuju direktno.
- **Profil trenutnog korisnika:** zaštićen je ceo, osim sadržaja Desktop, Documents, Downloads, Pictures, Music, Videos i OneDrive/SharePoint sync foldera (uključujući preusmerene). Ti folderi se sami ne preimenuju, ali se njihov sadržaj može obrađivati. `AppData` i sve ostalo u profilu je zaštićeno.
- **Stavke sa atributima Hidden + System** se preskaču.
- **Reparse point-i** (junction, symlink) se ne prate i ne preimenuju; u profilu postoje junction-i koji vode nazad u isti profil.
- **SUBST diskovi (npr. Short) i mrežni diskovi** se pre provere prevode na stvarnu putanju: `R:\` koji pokazuje u Program Files je i dalje zaštićen.
- Aplikacija radi **bez elevacije** i odbija rad ako je pokrenuta kao administrator.

## 5. Pregled pre izvršavanja

Pre preimenovanja prikazuje se tabela sa kolonama:

- Originalni naziv
- Predloženi naziv
- Status
- Rezultat provere

Statusi mogu biti:

- Spreman
- Bez promene
- Konflikt naziva
- Nevažeći naziv
- Greška pristupa

Preimenovanje se pokreće tek nakon potvrde korisnika.

Pravila provere:

- **Ne-ASCII znakovi koji nisu srpski** (`é`, `ü`, `–`, `„“`, emoji…) se ne menjaju. Stavka dobija status *Nevažeći naziv* i podrazumevano nije uključena; korisnik je može svesno uključiti (preimenuje se srpski deo, ostalo ostaje). Ako u nazivu nema ničeg srpskog za promenu, stavka se samo prijavljuje.
- **Pravila Windowsa** (zabranjeni znakovi, tačka ili razmak na kraju, rezervisani nazivi `CON`, `PRN`, `AUX`, `NUL`, `COM1–9`, `LPT1–9`, dužina naziva preko 255) daju *Nevažeći naziv* koji se ne može uključiti.
- **Konflikti** se proveravaju bez obzira na velika i mala slova (Windows ih ne razlikuje): `Šabac.docx` i `sabac.docx` u istom folderu su konflikt. Podrazumevano se konflikt ne razrešava automatski: stavka ostaje nepreimenovana.
- **Opcija „Konflikte reši sufiksom“** (podrazumevano isključena): umesto konflikta stavka dobija prvi slobodan naziv `Naziv (2).ext`, `Naziv (3).ext`… Postojeći fajlovi koji se ne preimenuju zadržavaju svoj naziv. Ako više stavki dobija isti naziv, osnovni naziv dobija prva po abecedi originalnog naziva, a ostale sufiks. Kod `.tar.gz`, `.tar.bz2`, `.tar.xz` sufiks ide ispred cele ekstenzije (`Arhiva (2).tar.gz`). Sufiks se vidi u koloni *Predloženi naziv* pre izvršavanja.
- **Dužina putanje:** `Đ`→`Dj`, `Љ`→`Lj`, `Џ`→`Dz` produžavaju naziv. Ako bi stavka ili nešto unutar preimenovanog foldera prešlo 260 znakova, stavka dobija upozorenje i podrazumevano nije uključena.
- Pre izvršavanja korisnik dobija upozorenje da preimenovanje može pokvariti prečice (`.lnk`), linkove između Office dokumenata i listu nedavnih fajlova.

## 6. Zaštita podataka

Aplikacija mora:

- sprečiti prepisivanje postojećih fajlova;
- otkriti konflikte između novih naziva;
- evidentirati neuspešne operacije;
- sačuvati originalne nazive radi mogućnosti povratka;
- omogućiti poništavanje uspešno izvršene operacije, kada su originalni nazivi i dalje dostupni.

Kod obrade poddirektorijuma, direktorijumi se preimenuju odozdo nagore kako se ne bi izgubile putanje do njihovog sadržaja.

**Dnevnik:** svaka uspešna promena se odmah upisuje u `%LOCALAPPDATA%\Cirlat\journal\<datum-vreme>.jsonl`, pa poništavanje radi i posle zatvaranja aplikacije, restarta ili pada. Poništavanje ide obrnutim redosledom (folderi odozgo nadole). Stavka koja ne može da se vrati (premeštena, ponovo preimenovana, originalni naziv zauzet) ostaje u dnevniku, a poništavanje se može ponoviti.

## 7. Korisnički interfejs

Glavni prozor sadrži:

- izbor direktorijuma;
- opciju uključivanja poddirektorijuma;
- opciju rešavanja konflikata sufiksom;
- izbor vrste stavki;
- dugme za analizu;
- tabelu predloženih promena;
- dugme za izvršavanje;
- dugme za poništavanje;
- traku napretka;
- pregled statistike i grešaka.

Ne postoji opcija za očuvanje srpskih dijakritika. Normalizacija je obavezna.

## 8. Tehnički kriterijumi prihvatanja

Aplikacija je spremna kada:

1. Ispravno obrađuje svih 30 slova srpske ćirilice.
2. Ispravno obrađuje `Lj`, `Nj` i `Dž`.
3. Pretvara `đ` u `dj`, a `џ` u `dz`.
4. Uklanja dijakritike iz svih obrađenih delova naziva.
5. Čuva sadržaj fajlova i strukturu direktorijuma.
6. Otkriva konflikte pre izvršavanja.
7. Ne prepisuje postojeće fajlove.
8. Prikazuje izveštaj o uspešnim i neuspešnim operacijama.
9. Proverava da konačni nazivi ispunjavaju definisani ASCII kriterijum.
10. Ne nudi, ne obilazi i ne menja ništa na zaštićenim lokacijama (4.1), bez obzira na to na kom disku su sistem i profili.
11. Odbija rad kada je pokrenuta sa administratorskim pravima.

## 9. Ograničenje naziva

CIRLAT u osnovnoj verziji ne menja proizvoljne specijalne znakove, ne uklanja automatski razmake i ne menja velika i mala slova, osim kada to zahteva transliteracija.

Strogi ASCII kriterijum odnosi se na slova i dijakritike; Windows dozvoljeni znakovi, dužina putanje i postojeći nazivi moraju se proveravati zasebno.

## 10. Princip rada

CIRLAT radi lokalno na Windows sistemu. Isporučuje se kao jedan `Cirlat.exe` (.NET 10, WinForms, self-contained), bez instalacije i bez instaliranog .NET-a. Ne šalje nazive niti sadržaj fajlova eksternim servisima i ne menja sadržaj dokumenata, slika, arhiva ili drugih datoteka.

