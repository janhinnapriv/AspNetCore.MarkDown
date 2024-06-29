# Introduction 
Det er veldig få onpremis løsninger for å samle markdown dokumentasjon (lokalt). Dette er en løsning som gir en mulighet til å liste wikier fra flere omåder. Det kan være enkeltfiler (md) eller katalogstrukturer hvor du lenker til en md-file under en katalog.   

# Getting Started
1.	Installasjon
2.	Programvareavhengiheter

## Installasjon
Publiser prosjektet ITDriftDok som en web-pakke og importer pakken i web-siten.
Filene må legges på forhåndsdefinerte områder
wwwroot/prost
wwwroot/wiki
Detter kan konfigureres
NB! Filene under wwwroot/wiki vil overskrives og i verste fall slettes. Ha en kopi av wiki-strukturen klar for kopiering.
## Programvareavhengiheter
Programmet bruker biblioteket Westwind.AspNetCore.Markdown
som ligger i prosjektet. Det er lagt til noen utvidelser.
## Wikistrukturen
Katalogen .Media eller .attachments brukes for å samle bilder og dokumenter som det skal lenkes til.
.order filen brukes om du ønsker en bestemt sortering på md-filene. Det bygges en innholdsside hvis katalogen med underliggende md-fil innholder kataloger med md-filer.
Du kan bruke html og bootstrap klasser i md-filene.  