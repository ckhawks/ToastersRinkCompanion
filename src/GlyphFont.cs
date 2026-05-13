using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

// Provides a Font that includes glyphs the B323 UI Toolkit default font is missing
// (e.g. U+2605 BLACK STAR). Use ApplyStarFont() on any Label whose text contains those
// characters so the glyph actually renders.
public static class GlyphFont
{
    private static Font _font;
    private static StyleFontDefinition _fontDef;
    private static bool _initialized;

    private static void EnsureLoaded()
    {
        if (_initialized) return;
        _initialized = true;

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (_font != null)
        {
            _fontDef = new StyleFontDefinition(FontDefinition.FromFont(_font));
            Plugin.Log($"GlyphFont: loaded '{_font.name}'");
        }
        else
        {
            Plugin.LogWarning("GlyphFont: no builtin font available; star glyphs may not render");
        }
    }

    public static void ApplyStarFont(Label label)
    {
        if (label == null) return;
        EnsureLoaded();
        if (_font == null) return;
        label.style.unityFontDefinition = _fontDef;
    }
}
