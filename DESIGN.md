---
name: EasyToDoList
description: A readable medieval pixel-art productivity companion built as a guild quest board.
colors:
  guild-ink: "#2A1B14"
  guild-ink-muted: "#654A36"
  parchment: "#F6E2B9"
  parchment-deep: "#E9C98F"
  wood: "#3A2418"
  wood-light: "#694228"
  cream: "#FFF2D0"
  gold: "#E2A93B"
  gold-pressed: "#C98B24"
  moss: "#526A3A"
  danger: "#9B3F32"
  disabled: "#9A876E"
typography:
  display:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "28px"
    fontWeight: 700
    lineHeight: 1.2
  heading:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "20px"
    fontWeight: 600
    lineHeight: 1.3
  body:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.5
rounded:
  control: "3px"
  panel: "5px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
components:
  button-parchment:
    backgroundColor: "{colors.parchment}"
    textColor: "{colors.guild-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "9px 16px"
  button-wood:
    backgroundColor: "{colors.wood-light}"
    textColor: "{colors.cream}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "9px 16px"
  panel-parchment:
    backgroundColor: "{colors.parchment}"
    textColor: "{colors.guild-ink}"
    rounded: "{rounded.panel}"
    padding: "24px"
---

# Design System: EasyToDoList

## Overview

**Creative North Star: "The Guild Quest Board"**

The interface should feel like a practical notice board inside a bright adventurer guild: tactile parchment and dark timber sit over a living pixel-art valley, while familiar Windows controls keep the utility immediate. Medieval flavor comes from material, palette, copy, and approved pixel art—not from making standard interactions strange.

The surface is task-first and restrained. Decorative animation is deferred until it has a defined state purpose; the foundational shell remains still, readable, and keyboard-operable.

**Key Characteristics:**

- High-contrast parchment and wood surfaces over approved pixel-art scenery.
- Compact, familiar controls with visible gold focus and hover feedback.
- One visual mode at a time: broad management board or compact overlay.
- Small, nearly square corners rather than soft modern pill shapes.

## Colors

The palette uses warm guild materials, with gold reserved for focus, selection, and primary interaction feedback.

### Primary

- **Guild Wood:** the structural surface for headers, rails, and Overlay framing.
- **Quest Gold:** focus outlines, interactive borders, and hover feedback; never paragraph text.

### Secondary

- **Guild Moss:** positive progress and calm focus-state emphasis.
- **Warning Crimson:** explicit destructive actions such as Exit.

### Neutral

- **Quest Ink:** default text on parchment.
- **Parchment:** primary reading and work surface.
- **Campfire Cream:** text on dark wood.

**The Two-Ink Rule.** Use Quest Ink on parchment and Campfire Cream on wood; never sample foreground text from the background illustration.

## Typography

**Display Font:** Segoe UI (system sans-serif fallback)
**Body Font:** Segoe UI (system sans-serif fallback)

**Character:** Clear Windows utility typography carries the dense product work. Pixel-art identity comes from the surrounding world; labels and data remain immediately readable.

### Hierarchy

- **Display** (700, 28px, 1.2): shell title only.
- **Heading** (600, 20px, 1.3): panel and mode headings.
- **Body** (400, 14px, 1.5): labels, explanatory copy, and data.

**The Utility First Rule.** Do not use decorative medieval lettering for buttons, form labels, timers, or task text.

## Layout

Dashboard uses a header above a three-region board: journey navigation, primary task surface, and narrow status rail. Its window size is capped to the current Windows work area. Overlay uses the same window at a compact fixed width and keeps its two submodes inside one framed surface.

Spacing follows a 4/8/16/24/32 rhythm. Related controls use 8px gaps; panels separate by 24px; the Dashboard canvas uses 32px outer spacing when the work area permits.

## Elevation & Depth

Depth comes from solid material layering and three-pixel contrasting borders, not blur or glass. The landscape remains behind an unobtrusive dark scrim so foreground surfaces retain contrast.

**The Flat Board Rule.** Panels remain flat at rest; do not add soft card shadows on top of their structural borders.

## Shapes

Controls use compact three-pixel corners and panels use five-pixel corners. Borders are deliberate and structural, reflecting framed timber and mounted parchment rather than modern floating cards.

## Components

### Buttons

- **Shape:** compact framed control (3px corner, 2px border, minimum 40px height).
- **Primary:** parchment with Quest Ink for actions on dark surfaces.
- **Wood:** lighter wood with Campfire Cream and gold border for mode changes.
- **Hover / Focus:** gold fill on hover; two-pixel dashed gold focus rectangle for keyboard users.
- **Danger:** crimson surface with cream text, only for explicit destructive actions.

### Cards / Containers

- **Corner Style:** framed board (5px corner).
- **Background:** parchment for reading/work; wood for shell framing and status rails.
- **Border:** three-pixel wood or gold structural line.
- **Internal Padding:** 24px for primary panels, 20px for narrow rails.

### Navigation

Dashboard navigation is a short vertical journey list on parchment. The active entry is distinguished by weight as well as color; M07 will add its interaction states when navigation is wired.

## Do's and Don'ts

### Do:

- **Do** keep functional text on an opaque parchment or wood surface.
- **Do** preserve nearest-neighbor rendering for approved pixel-art assets.
- **Do** keep Dashboard and Overlay visually related through the same material and control vocabulary.

### Don't:

- **Don't** place body text directly on the Guild Valley illustration.
- **Don't** use gold as decorative body copy or a large inactive surface.
- **Don't** add ambient or mascot motion before Reduced Motion and state mapping are implemented.
