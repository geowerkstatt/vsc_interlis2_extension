using Geowerkstatt.Interlis.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class ReactFlowVisitorTests
{
    // ─── helpers ────────────────────────────────────────────────────────────────
    private static ReactflowResponse BuildDiagram(string ili)
    {
        var reader = new InterlisReader();
        var ast = reader.ReadFile(new StringReader(ili));
        var visitor = new ReactFlowVisitor(NullLogger<ReactFlowVisitor>.Instance);
        visitor.VisitInterlisFile(ast);
        return visitor.GetDiagramDocument();
    }


    [TestMethod]
    public void SingleClass_InTopic_ProducesNamespaceAndClass()
    {
        var diagram = BuildDiagram(@"
           INTERLIS 2.4;

!!@ Title = ""Kantonale Sondernutzungspläne""
!!@ shortDescription = ""Das kantonale Geodatenmodell Kantonale Sondernutzungspläne beschreibt die gemäss PBG Art. 32 erlassenen Sondernutzungspläne.""
!!@ Issuer = http://www.areg.sg.ch
!!@ technicalContact = mailto:geodaten@sg.ch
!!@ furtherInformation = http://www.geoinformation.sg.ch
!!@ IDkGeoIV = ""69-SG""
!!@ kGeoIV_Kategorie = ""IV""
!!@ kGeoIV_Zustaendigkeit = ""AREG""
!!@ eCH_Kategorie_Nr = 151
!!@ eCH_Kategorie_Name = ""Raumplanung;Raumentwicklung""
!!@ Modelltyp = ""Publikation""
!!@ kGDI_Kuerzel = ""kSNP""
!!@ Compilerversion = ""4.7.7-20180208""

!! Bemerkungen:
!!
!! Änderungs-Historie:
!! 2018-09-20 / 0.9.0 / Erstfassung des Modells
!! 2018-11-30 / 1.0.0 / Publikation des Modells
!! 2022-10-01 / 1.1.0 / Ergänzung Attribut 'Objektnummer' und 'Gewässername' in der Klasse SNP_Basis
!! 2023-06-30 / 1.2.0 / Ergänzung Codeliste um Gewässerraum-Codes nach WBG; Ergänzung der Status 'projektiert.im_Mitwirkungsverfahren' und 'rechtskraeftig.Aufhebung_im_Mitwirkungsverfahren'

MODEL SG_Sondernutzungsplaene_Codelisten_V1_2_0
  AT ""https://models.geo.sg.ch""
  VERSION ""2023-06-30"" =

  TOPIC Kt_Codelisten =
    !!@ geow.uml.color = ""#AECFFF""
    !!@ geow.doc.description = ""Diese Klasse beinhaltet die Objekteinteilung der kantonalen Sondernutzungspläne inkl. Baulinientypen und ist im separaten Modell «SG_Sondernutzungsplaene_kt_V1_2_0» ausgegliedert.""
    CLASS SNP_Code =
      !!@ geow.doc.description = ""Code des kantonalen Festlegungstyps""
      Code : MANDATORY 1000 .. 9999;
      !!@ geow.doc.description = ""Kürzel des kantonalen Festlegungstyps""
      Kuerzel : MANDATORY TEXT*12;
      !!@ geow.doc.description = ""Bezeichnung des kantonalen Festlegungstyps""
      Bezeichnung : MANDATORY TEXT*80;
    END SNP_Code;

  END Kt_Codelisten;
END SG_Sondernutzungsplaene_Codelisten_V1_2_0.

!!@ geow.doc.title = ""Einleitung""
!!@ geow.doc.description = ""Diese Modelldokumentation beschreibt das kantonale Geodatenmodell (kGDM) für folgende Geobasisdatensätze gemäss kantonalem Geobasisdatenkatalog: 69-SG Kantonale Sondernutzungspläne. Diese Dokumentation richtet sich an Fachleute, welche kantonale Geobasisdaten verwenden oder sich mit der Modellierung kantonaler Geobasisdaten befassen. Struktur und Inhalte des in INTERLIS 2.3 beschriebenen Datenmodells werden in dieser Dokumentation mit Hilfe eines UML-Klassendiagramms und einem Objektkatalog erläutert. Auf eine Weisung und Erfassungsrichtlinien zum Datenmodell kantonale Sondernutzungspläne wird verzichtet. Für die Datenerfassung sind die Vorgaben für kommunale Sondernutzungspläne der Weisung und Erfassungsrichtlinien zum Datenmodell kommunale Nutzungsplanung sinngemäss anzuwenden.""
MODEL SG_Sondernutzungsplaene_kt_V1_2_00
  AT ""https://models.geo.sg.ch""
  VERSION ""2023-06-30"" =
    IMPORTS SG_Sondernutzungsplaene_Codelisten_V1_2_0;
    IMPORTS SG_Basis_kt_V1_0_0;


  !!@ geow.doc.description = ""Das Topic Transfermetadaten beinhaltet Informationen zum gelieferten Datenbestand und zur für die Datenbearbeitung zuständigen Stelle. Die Transfermetadaten stellen sicher, dass der Inhalt jeder Datenlieferung eindeutig beschrieben ist.""
  TOPIC Transfermetadaten =
    !!@ geow.doc.description = ""Diese Klasse enthält Angaben zur zuständigen Stelle, welche die Geobasisdaten bearbeitet hat.""
    !!@ geow.uml.color = ""#ffdc97""
    CLASS Stelle =
      !!@ geow.doc.description = ""Name der bearbeitenden Stelle""
      !!@ geow.doc.alias = ""Name""
      !!@ geow.doc.limitation = ""P""
      Name : MANDATORY TEXT*80;
      !!@ geow.doc.description = ""Verweis auf Webseite der Stelle""
      !!@ geow.doc.alias = ""Stelle im Web""
      !!@ geow.doc.limitation = ""P""
      Stelle_im_Web : TEXT*80;
    END Stelle;

    !!@ geow.uml.color = ""#c4dea4""
    CLASS Datenbestand =
      !!@ geow.doc.alias = ""Gegenstand""
      !!@ geow.doc.limitation = ""P""
      Gegenstand : MANDATORY TEXT*250;
      !!@ geow.doc.alias = ""Stand""
      !!@ geow.doc.limitation = ""P""
      Stand : MANDATORY XMLDate;
      !!@ geow.doc.alias = ""Lieferdatum""
      !!@ geow.doc.limitation = ""P""
      Lieferdatum : MANDATORY XMLDate;
      !!@ geow.doc.alias = ""Bemerkung""
      !!@ geow.doc.limitation = ""P""
      Bemerkung : TEXT*250;
    END Datenbestand;

    ASSOCIATION zustStelle_Daten =
      !!@  geow.doc.description = ""Verweis zur zuständigen Stelle""
      Datenbestand -- {0..*} Datenbestand;
      zustaendigeStelle -<> {1} Stelle;
    END zustStelle_Daten;

  END Transfermetadaten;

  TOPIC Rechtsvorschriften =

    !!@ geow.uml.color = ""#febecc""
    CLASS Dokument =
      !!@ geow.doc.alias = ""Titel""
      !!@ geow.doc.limitation = ""P""
      Titel : MANDATORY TEXT*80;
      !!@ geow.doc.alias = ""Text im Web""
      !!@ geow.doc.limitation = ""P""
      Text_im_Web : TEXT*80;
      !!@ geow.doc.alias = ""Bemerkung""
      !!@ geow.doc.limitation = ""P""
      Bemerkung : TEXT*250;
    END Dokument;

  END Rechtsvorschriften;

  TOPIC Sondernutzungsplaene =
    DEPENDS ON
      SG_Sondernutzungsplaene_kt_V1_2_0.Rechtsvorschriften,
	  SG_Sondernutzungsplaene_Codelisten_V1_2_0.Kt_Codelisten;

    !!@ geow.uml.color = ""#fff9e""
    !!@ geow.doc.description = ""Diese Klasse ist eine abstrakte Klasse. Sie enthält die gemeinsamen Attribute der geometrischen Klassen zum Sondernutzungsplan. Sie wird durch die geometrischen Klassen SNP_Perimeter, SNP_Baulinie, SNP_Flaeche und SNP_Linie erweitert.""
    CLASS SNP_Basis (ABSTRACT) =
      !!@ geow.doc.description = ""Kantonale konstante und eindeutige Nummer""
      !!@ geow.doc.alias = ""Objektnummer""
      !!@ geow.doc.limitation = ""P""
      Objektnummer : MANDATORY TEXT*12;
      !!@ geow.doc.description = ""Bezeichnung des Gewässers""
      !!@ geow.doc.alias = ""Gewässername""
      !!@ geow.doc.limitation = ""P""
      Gewaessername : TEXT*250;
      !!@ geow.doc.description = ""Angabe zum Rechtsstatus""
      !!@ geow.doc.alias = ""Status""
      !!@ geow.doc.limitation = ""P""
      Status : MANDATORY Rechtsstatus;
      !!@ geow.doc.description = ""Datumsangabe zum Entwurf""
      !!@ geow.doc.alias = ""Datum Entwurf""
      !!@ geow.doc.limitation = ""P""
      Datum_Entwurf : XMLDate;
      !!@ geow.doc.description = ""Datum zur Auflage""
      !!@ geow.doc.alias = ""Datum Auflage""
      !!@ geow.doc.limitation = ""P""
      Datum_Auflage : XMLDate;
      !!@ geow.doc.description = ""Datum zum Erlass""
      !!@ geow.doc.alias = ""Datum Erlass""
      !!@ geow.doc.limitation = ""P""
      Datum_Erlass : XMLDate;
      !!@ geow.doc.description = ""Rechtskraftdatum""
      !!@ geow.doc.alias = ""Datum Rechtskraft""
      !!@ geow.doc.limitation = ""P""
      Datum_Rechtskraft : XMLDate;
      !!@ geow.doc.description = ""Aufhebungsdatum""
      !!@ geow.doc.alias = ""Datum Aufhebung""
      !!@ geow.doc.limitation = ""V""
      Datum_Aufhebung : XMLDate;
      !!@ geow.doc.description = ""Erläuternder Text oder Bemerkungen""
      !!@ geow.doc.alias = ""Bemerkung""
      !!@ geow.doc.limitation = ""P""
      Bemerkung : TEXT*250;
    END SNP_Basis;

    ASSOCIATION SNP_Basis_Vorschrift =
      !!@ geow.doc.description = ""Liste der Rechtsvorschriften und Dokumente, welche diesem Sondernutzungsplan zugeordnet sind (Fremdschlüssel).""
      Sondernutzungsplanobjekt -- {0..*} SNP_Basis;
      Vorschrift (EXTERNAL) -- {0..*} SG_Sondernutzungsplaene_kt_V1_2_0.Rechtsvorschriften.Dokument;
    END SNP_Basis_Vorschrift;

    ASSOCIATION SNP_Code_SNP_Basis =
      !!@ geow.doc.description = ""Zugehöriger Typ des Sondernutzungsplanobjekts (Fremdschlüssel).""
      Sondernutzungsplanobjekt -- {0..*} SNP_Basis;
      SNP_Code (EXTERNAL) -<> {1} SG_Sondernutzungsplaene_Codelisten_V1_2_0.Kt_Codelisten.SNP_Code;
    END SNP_Code_SNP_Basis;

    !!@ geow.uml.color = ""#fff9ae""
    !!@ geow.doc.description = ""Diese Klasse ist eine Erweiterung der Klasse SNP_Basis für die Abbildung der Perimeter von Sondernutzungsplänen. Diese sind Geometrien vom Typ Einzelfläche (SGFlaeche2DKreisbogen).""
    CLASS SNP_Perimeter
    EXTENDS SNP_Basis =
      !!@ geow.doc.description = ""Geometrieattribut""
      Geometrie : MANDATORY SGFlaeche2DKreisbogen;
    END SNP_Perimeter;

    !!@ geow.uml.color = ""#fff9ae""
    !!@ geow.doc.description = ""Diese Klasse ist eine Erweiterung der Klasse SNP_ Basis für die Abbildung der Baulinien aus Sondernutzungsplänen. Diese sind als Linie (SGLinie2DKreisbogen) definiert.""
    CLASS SNP_Baulinie
    EXTENDS SNP_Basis =
      !!@ geow.doc.description = ""Angabe zur Wirkung der Baulinie""
      !!@ geow.doc.alias = ""Wirkung""
      Wirkung : MANDATORY WirkungBaulinie;
      !!@ geow.doc.description = ""Geometrieattribut""
      Geometrie : MANDATORY SGLinie2DKreisbogen;
    END SNP_Baulinie;

    !!@ geow.uml.color = ""#fff9ae""
    !!@ geow.doc.description = ""Diese Klasse ist eine Erweiterung der Klasse SNP_Basis für weitere flächenförmige Inhalte von Sondernutzungsplänen. Diese sind Geometrien vom Typ Einzelfläche (SGFlaeche2DKreisbogen).""
    CLASS SNP_Flaeche
    EXTENDS SNP_Basis =
      !!@ geow.doc.description = ""Geometrieattribut""
      Geometrie : MANDATORY SGFlaeche2DKreisbogen;
    END SNP_Flaeche;

    !!@ geow.uml.color = ""#fff9ae""
    !!@ geow.doc.description = ""Diese Klasse ist eine Erweiterung der Klasse SNP_Basis für weitere linienförmige Inhalte von Sondernutzungsplänen.Diese sind Geometrien vom Typ Einzelfläche (SGLinie2DKreisbogen).""
    CLASS SNP_Linie
    EXTENDS SNP_Basis =
      !!@ geow.doc.description = ""Geometrieattribut""
      Geometrie : MANDATORY SGLinie2DKreisbogen;
    END SNP_Linie;

  END Sondernutzungsplaene;

END SG_Sondernutzungsplaene_kt_V1_2_0.
");

        Debug.WriteLine("Hi");
    }
}
