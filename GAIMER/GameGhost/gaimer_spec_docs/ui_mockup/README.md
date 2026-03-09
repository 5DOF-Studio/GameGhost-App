# Witness Desktop - UI Mockup

Interactive HTML/CSS/JS prototype for the Witness Desktop application.

## Purpose

This mockup serves as:
1. **Visual reference** for .NET MAUI development
2. **Design iteration tool** - test changes quickly in browser
3. **UI specification** - final approved design becomes the spec

## Running Locally

Simply open `index.html` in a browser, or use a local server:

```bash
# Using Python
python -m http.server 8080

# Using Node.js
npx serve .

# Using PHP
php -S localhost:8080
```

Then open http://localhost:8080

## Features Demonstrated

- ✅ App window selector with thumbnails
- ✅ Connection state management (offline/connecting/connected)
- ✅ Window preview area with HUD overlay
- ✅ Audio visualizer with animated bars
- ✅ Volume level indicators
- ✅ Toast notifications
- ✅ Keyboard shortcuts

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Ctrl/Cmd + Enter` | Connect/Disconnect |
| `Escape` | Disconnect |
| `↑` / `↓` | Navigate app list |

## Design Tokens

All design values are CSS variables in `styles.css`:

```css
--bg-primary: #050505
--bg-secondary: #0f172a
--accent-cyan: #06b6d4
--accent-purple: #a855f7
--text-primary: #e0e0e0
```

## Files

| File | Purpose |
|------|---------|
| `index.html` | Page structure |
| `styles.css` | All styling (design system) |
| `app.js` | Interactivity & mock data |

## Next Steps

1. Review and refine the UI design
2. Add any missing states/interactions
3. Export final design tokens for MAUI
4. Create XAML templates based on approved design

