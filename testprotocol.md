# Testprotokoll

| Testfall | Fokusfunktion | Handlungsanweisung (Testschritt) | Erwartetes Ergebnis |
| -------- | ------- | ------- | ------- |
| 1 | Snippets | In ILI-Datei (/examples/Test1.ili) ein neues CLASS Element eröffnen | - Ending-Tag wird ergänzt <br> - Klassenname kann gesetzt werden und er wird bei Eröffnung und Beendigung eingetragen |
| 2 | Syntax Highlight | ILI Datei öffnen (/examples/DMAV_Bodenbedeckung_V1_0.ili) | - INTERLIS Schlüsselwörter sind alle eingefärbt |
| 3 | UML Diagramm Darstellung | ILI Datei öffnen (/examples/LWB_Nutzungsflaechen_V3_0.ili) und UML Diagramm anzeigen (CTRL+SHIFT+P > Show INTERLIS Diagram View) | - UML wird als Preview angezeigt komplett gemäss der folgenden Abbildung: ![alt text](/images/uml.png) |
| 4 | Code Navigation | ILI Datei öffnen (/examples/DMAV_Bodenbedeckung_V1_0.ili) und auf Zeile 27 Cursor innerhalb des importierten Modells platzieren und Kontextmenu-Aufruf 'Go To Definition' ausführen  | - Die Modelldatei DMAVTYM_Geometrie_V1_0.ili wird angezeigt |
| 5 | Modell-Doku | ILI Datei öffnen (/examples/DMAV_Bodenbedeckung_V1_0.ili) und mit CTRL+SHIFT+P > INTERLIS 2: Generate markdown documentation ausführen  | - Die tabellarische Dokumentation wird als Markdown-Datei angezeigt. Der Preview entspricht bei der Klasse Bodenbedeckung der folgenden Darstellung: ![alt text](/images/docu.png) |