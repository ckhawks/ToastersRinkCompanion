# 🏒 Toaster's Rink Companion — Changelog

## Major Features

### Companion Panel

- Complete UI overhaul — new tabbed panel with **Home, Players, Training, Servers, Donors, and Admin** tabs
- **Server Browser** tab pulling live servers from puckstats.io with ping display
- **Donors** tab showing supporter Steam avatars and Ko-fi link
- **Players** tab with Steam avatars, donor badges, and EIS team pills with logos
- **Quick-Join** buttons for position select (blue/red skater/goalie)
- **Credits** section on the Home tab

### Modifier System

- **Modifier Panel (F3):** full tabbed UI with collapsible categories and an active modifiers section
- **Vote Popup:** timer bar, vote bar with yes/no/threshold visualization, argument display
- **Active Modifiers HUD:** category-colored text with dots, sorted by category on screen
- **Settings Tab:** configurable keybinds for vote yes/no, panel toggle, and spawn puck
- Dynamic argument controls — sliders, dropdowns, team pickers, player pickers
- 9 modifier categories with color coding
- Modifier search filter in the panel

### AI Goalies

- Added AI goalies (thanks @Ami for lending yours as a starting point) that automatically fill empty goalie slots during games
- Upgraded goalie personality
- Added **Disable AI Goalies** modifier to turn them off via vote

### Single Goalie Rework

- Single goalie mode is now always active by default — auto-detects when a team has a lone goalie
- Goalie teleports and switches team instead of mirroring
- Renamed modifier to **Disable Single Goalie** to opt out
- Stamina resets on respawn
- Camera overlay suppressed during team switches

---

## New Content

### New Modifiers

- **Free Blade** modifier (serve up pucks)
- **High Sticking** modifier (it's not what you think)
- **Disable Goal Explosion** modifier (who would want that)
- **Look Up Turn** modifier (for the HQMers who can't break the muscle memory (me))

### Rock Event

- Rock event now has a 5-minute despawn timer with countdown under the boss bar
- Timer changes color at 60s (yellow) and 30s (red)

---

## Improvements

### Player Stats

- Added live player stats panel — expand any player row to see goals, assists, touches, time played, movement, and juggle count
- Stats update every 5 seconds

### Minimap

- Minimap now renders portals, ramps, walls, cones, pillars, jails, rocks, and goals as shapes with proper layering

### Meme of the Day

- Rink now features a meme of the day that you can like or dislike with your stick or puck

---

## Quality of Life & Fixes

- Added **/watchpucksof** command to spectate another player's pucks
- Added admin jail system
- Enhanced love

## Plus a handful of other little things to discover!

---

## Under the Hood

- Full migration to Puck build 312 API (80+ files updated)
- Fixed juggle rally timer, updatable chat, boss bar UI, goal explosions, portal teleportation, and warmup transitions
- Fixed physics tick rate for puck timers (now proper 50Hz)
- Various build stability fixes
- Fixed "physics explosion" bug
- Fixed pucks not syncing position bug
- Fixed `/bp` and `/bp a` not always working
- Fixed rock boss bar intro animation getting paused if rock hit before animation finished

--

## Todo still

- Noob-only / Noob-friendly server
- CompTweaks everything (Vanilla, PHL Tweaks, OPL Tweaks)
- DalfStats integration, PuckStats.io season 2
- TRL XP Boost
- Rework ban/mute system
- Add MOTD UI
