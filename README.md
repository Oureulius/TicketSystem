# TicketSystem

Desktopová aplikace pro správu ticketů

## Hlavní funkcionality
- přihlášení uživatele pomocí `loginu` a hesla
- vytvoření nového ticketu
- přehled posledních ticketů na dashboardu
- zobrazení detailu ticketu
- filtrování ticketů podle priority, kategorie a tvůrce
- správa uživatelů administrátorem
- lokální databáze `SQLite`
- oddělení oprávnění pro běžného uživatele a administrátora
- statistiky ticketů pro administrátora
- graf vývoje ticketů v čase

## Uživatelský dashboard
Běžný uživatel vidí pouze své tickety a tickety, které má přidělené.

### Co obsahuje
- přehled základních údajů o ticketech
- poslední tickety
- graf počtu ticketů za posledních 7 dní
- vytvoření nového ticketu
- detail vlastních a přidělených ticketů

### Vzhled uživatelského dashboardu
<img width="1320" height="851" alt="image" src="https://github.com/user-attachments/assets/f18fc593-dcfc-422b-9fb8-21c859f0adb7" />


## Admin dashboard
Administrátor má plný přístup ke všem ticketům i k uživatelům.

### Co obsahuje navíc
- přehled všech ticketů
- filtrování všech ticketů
- správa uživatelů
- vytváření nových uživatelů
- mazání uživatelů
- statistiky ticketů

## Statistiky administrátora
Administrátorská sekce obsahuje přehledné statistiky práce s tickety.

### Zobrazuje například
- počet ticketů za poslední 1 den
- počet ticketů za posledních 14 dní
- počet ticketů za posledních 30 dní
- počet ticketů za posledních 365 dní
- graf ticketů podle času
- nejčastější kategorie/problémy

### Vzhled uživatelského dashboardu
<img width="1320" height="849" alt="image" src="https://github.com/user-attachments/assets/56a723c0-cf7e-4c1e-b2a5-597345650652" />

### Vzhled admin statistiky
<img width="1319" height="848" alt="image" src="https://github.com/user-attachments/assets/a11a23d8-354e-4c57-9741-9ec3699c66aa" />


## Správa uživatelů
Administrátor může:
- vytvářet nové uživatele
- zobrazit seznam uživatelů
- mazat vybrané uživatele

## Přihlášení
Aplikace obsahuje oddělené role:
- `User`
- `Admin`

## Databáze
Data jsou ukládána lokálně do souboru `SQLite` databáze.

## Výchozí účty
Pokud je aplikace spuštěna poprvé, vytvoří se výchozí uživatelé:
- `Admin`
- `User`

## Technologie
- `.NET 10`
- `C#`
- `Avalonia UI`
- `SQLite`
- `LiveCharts`
