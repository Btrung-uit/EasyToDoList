using System.IO;
using System.Xml.Linq;

namespace EasyToDoList.App.Tests.Features;

public sealed class AccessibilityTests
{
    [Theory]
    [InlineData("GuildInkColor", "GuildParchmentColor")]
    [InlineData("GuildInkMutedColor", "GuildParchmentColor")]
    [InlineData("GuildCreamColor", "GuildWoodColor")]
    [InlineData("GuildCreamColor", "GuildWoodLightColor")]
    [InlineData("GuildCreamColor", "GuildDangerColor")]
    [InlineData("GuildDangerColor", "GuildParchmentColor")]
    [InlineData("GuildInkColor", "GuildGoldColor")]
    [InlineData("GuildInkColor", "GuildGoldPressedColor")]
    public void Active_text_palette_has_wcag_aa_contrast(string foreground, string background)
    {
        static double Luminance(string hex)
        {
            var channels = new[] { 1, 3, 5 }.Select(offset => Convert.ToInt32(hex.Substring(offset, 2), 16) / 255d)
                .Select(channel => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4)).ToArray();
            return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
        }
        var palette = XDocument.Load(Path.Combine(SourceDirectory(), "Themes", "Colors.xaml")).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(element => (string)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))!, element => element.Value);
        var a = Luminance(palette[foreground]);
        var b = Luminance(palette[background]);
        Assert.True((Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05) >= 4.5);
    }

    [Fact]
    public void Inputs_and_data_lists_have_explicit_accessible_names()
    {
        var inputs = new[] { "TextBox", "DatePicker", "Slider", "ListBox", "DataGrid" };
        var missing = Directory.EnumerateFiles(SourceDirectory(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => inputs.Contains(element.Name.LocalName)
                    && string.IsNullOrWhiteSpace((string?)element.Attribute("AutomationProperties.Name")))
                .Select(element => $"{Path.GetFileName(path)}: {element.Name.LocalName}"));
        Assert.Empty(missing);
    }

    private static string SourceDirectory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "EasyToDoList.sln"))) root = root.Parent;
        Assert.NotNull(root);
        return Path.Combine(root.FullName, "src", "EasyToDoList.App");
    }
}
