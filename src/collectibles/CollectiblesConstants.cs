using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.collectibles;

public static class CollectiblesConstants
{
    private static Dictionary<string, Color> RARITY_COLORS = new Dictionary<string, Color>()
    {
        { "Common", new Color(0.7f, 0.7f, 0.7f) },
        { "Uncommon", new Color(0.353f, 0.761f, 0.322f) },
        { "Rare", new Color(0.004f, 0.592f, 0.859f) },
        { "Epic", new Color(0.482f, 0.031f, 0.976f) },
        { "Legendary", new Color(1, 0.639f, 0.082f) },
        { "Mythic", new Color(0.922f, 0.2f, 0.239f) },
    };

    public static Dictionary<string, string> ITEM_PATHS = new Dictionary<string, string>()
    {
        { "Baseball Bat", "baseball_bat.fbx"},
        { "Bomb", "bomb.fbx" },
        { "Book", "book.fbx" },
        { "Cone", "cone.fbx" },
        { "Crowbar", "crowbar.fbx" },
        { "Door", "door.fbx"},
        { "Cinder Block", "cinderblock.fbx" },
        { "Error", "error.fbx" },
        { "G3A3", "g3a3.fbx" },
        { "Glass of Milk", "glassofmilk.prefab" },
        { "Goal", "Goal.prefab" },
        { "Goalie Helmet", "helmet_goalie.fbx" },
        { "Lightbulb", "lightbulb.prefab" },
        { "Melvin", "melvin.fbx"},
        { "M40A5", "m40a5.fbx" },
        { "MP9", "mp9.fbx" },
        { "Mug", "mug.fbx" },
        { "Skater Helmet", "helmet_skater.fbx" },
        { "Skateboard", "skateboard.fbx"},
        { "Stick", "stick.fbx"},
        { "Toaster", "toaster.fbx" },
        { "Tuna Fish", "tuna-fish.fbx" },
    };
    
    public static Dictionary<string, string> PATTERN_PATHS = new Dictionary<string, string>()
    {
        { "Default", null}, // Use null for default (no custom texture)
        { "Abyssal Foam", "abyssal_foam.png"},
        { "Auburn Kryptek", "kryptek auburn.png" },
        { "Beach", "gradient_beach.png" },
        { "Blue Marbled", "marbled_blue.png" },
        { "Blurred Grass", "blurred_grass.png" },
        { "Burnt", "burnt.png" },
        { "Canyon", "canyon.png" },
        { "Case Hardened", "case_hardened.png" },
        { "Chroma Burst", "chroma_burst.png"},
        { "Chrome Lattice", "chrome_lattice.png"},
        { "Chrome Obsidian", "chrome_obsidian.png"},
        { "Color Grid", "color_grid.png" },
        { "Cosmic Waves", "cosmic_waves.png" },
        { "Daisy Bloom", "daisy_bloom.png" },
        { "Damascus", "damascus.jpg" },
        { "Denim", "denim.png"},
        { "Digital Reef", "digital reef.png" },
        { "Dotted Wave", "dotted_wave.png"},
        { "Error", "error.png" },
        { "Exotic Polychrome", "exotic_polychrome.png" },
        { "Fade", "fade.png" },
        { "Flesh", "flesh.png" },
        { "Interior Rose", "interior rose.png" },
        { "Iridescent", "iridescent.png"},
        { "Mandrake Kryptek", "kryptek mandrake.png" },
        { "Misanthrope", "misanthrope.png" },
        { "Missing", "missing.png" },
        { "Muted Cyan Kryptek", "kryptek muted cyan.png" },
        { "Nightlife", "nightlife.png" },
        { "Orange Tetris", "tetris_orange.png" },
        { "Peach", "gradient_peach.png" },
        { "Pink Bloom", "pink_bloom.png" },
        { "Pink Bubbles", "bubbles_pink.png" },
        { "Pink Tetris", "tetris_pink.png" },
        { "Prism Pool", "prism_pool.png"},
        { "Prismatic", "prismatic.png" },
        { "Purple Kryptek", "kryptek purple.png" },
        { "Pastel Rainbow", "rainbow_gradient.jpg" },
        { "Red Splatter", "red-splatter.jpg" },
        { "RGB Digital", "rgb-digital.png" },
        { "Rink", "hockey_rink.png" },
        { "Rusted Fire", "rusted fire.png" },
        { "Scarlet", "scarlet.png" },
        { "SK8", "sk8.png" },
        { "Solitude", "solitude.png"},
        { "Orange Stain", "stain_orange.png"},
        { "Red Stain", "stain_red.png"},
        { "Yellow Stain", "stain_yellow.png"},
        { "Titanium", "gradient_titanium.png" },
        { "Umbra", "umbra.png"},
        { "Wavy Grass", "wavy_grass.png" },
    };
    
    public static Color GetColor(string rarityName)
    {
        if (RARITY_COLORS.TryGetValue(rarityName, out Color color))
        {
            return color;
        }
        // Fallback for unexpected rarities
        return Color.white; 
    }
}