using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Settings;

namespace EarSaxophone.Core.Tests;

public class StyleSettingTests
{
    [Fact]
    public void DefaultStyleIsStudio()
    {
        Assert.Equal(AppStyle.Studio, new AppSettings().Style);
    }

    [Fact]
    public void StyleSurvivesSaveAndLoad_AndCountsAsAChange()
    {
        string path = Path.Combine(Path.GetTempPath(), "earsax-style-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings(Style: AppStyle.Messing);

            store.Save(settings);

            Assert.Equal(AppStyle.Messing, store.Load().Style);
            Assert.NotEqual(new AppSettings(), settings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EveryStyleHasADistinctGermanName()
    {
        string[] names = Enum.GetValues<AppStyle>().Select(Texts.StyleName).ToArray();

        Assert.Equal(8, names.Length);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void OldSettingsFileWithoutStyleLoadsWithTheDefault()
    {
        string path = Path.Combine(Path.GetTempPath(), "earsax-style-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ \"showOsd\": false }");

            AppSettings loaded = new SettingsStore(path).Load();

            Assert.False(loaded.ShowOsd);
            Assert.Equal(AppStyle.Studio, loaded.Style);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
