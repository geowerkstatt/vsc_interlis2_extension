namespace Geowerkstatt.Interlis.LanguageServer;

[TestClass]
public class UiLanguageContextTest
{
    [TestMethod]
    public void Resolve_ExplicitLanguage_WinsOverFallback()
    {
        var ctx = new UiLanguageContext { Language = DocumentationLocalization.French };

        Assert.AreEqual(DocumentationLocalization.English, ctx.Resolve(DocumentationLocalization.English));
    }

    [TestMethod]
    public void Resolve_AutoSentinel_FallsBackToUiLanguage()
    {
        var ctx = new UiLanguageContext { Language = DocumentationLocalization.Italian };

        Assert.AreEqual(DocumentationLocalization.Italian, ctx.Resolve(UiLanguageContext.AutoLanguage));
    }

    [TestMethod]
    public void Resolve_EmptyOrNull_FallsBackToUiLanguage()
    {
        var ctx = new UiLanguageContext { Language = DocumentationLocalization.French };

        Assert.AreEqual(DocumentationLocalization.French, ctx.Resolve(null));
        Assert.AreEqual(DocumentationLocalization.French, ctx.Resolve(string.Empty));
    }

    [TestMethod]
    public void Resolve_DefaultUiLanguageIsGerman()
    {
        var ctx = new UiLanguageContext();

        Assert.AreEqual(DocumentationLocalization.German, ctx.Resolve(UiLanguageContext.AutoLanguage));
    }
}