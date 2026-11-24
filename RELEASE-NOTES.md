RMPCS v0.11.0 - Rusty's PC Toolkit

A cleaner interface with bundled typography, clearer navigation and consistent themed controls.

- Embedded fonts: Barlow (default), Rajdhani and Atkinson Hyperlegible, plus Windows system. Choose in Settings > Fonts & appearance. Font and color choices preview immediately; Save appearance keeps them and Discard preview restores the saved choices. Fonts are loaded from the application resources, work offline and are never installed into Windows. Full copyright/OFL license files and THIRD-PARTY-NOTICES.md ship with installer and portable packages. Source files and provenance hashes are included in the source bundle.
- Back navigation: dedicated Back buttons in desktop and sidebar layouts, Alt+Left and the mouse Back button navigate through visited sections, including sidebar Results & activity. Navigation never replays a task, cancels active work or undoes task selections. Quit RMPCS is a separate, explicitly labeled action away from maintenance buttons. Existing close/minimize-to-tray and active-task exit guards are preserved.
- Visual refinement: a quieter navigation rail, consistent Overview cards, balanced page/results proportions, rounded task checkboxes, dropdowns and expanders, and themed text/report areas. Read-only text remains selectable and scrollable. Focus indicators and High Contrast color overrides remain intact. Selected navigation font weights/colors no longer leak into page content.
- Settings groups separate Fonts & appearance, Sidebar & displays, Window/notifications/defaults, GitHub updates and Installation/automation. The Updates shortcut opens the relevant group. In-app help explains the new navigation, fonts and licenses.
- Font settings are allowlisted data, never font paths or URLs; malformed values normalize to the bundled default. No new network service, background task, maintenance option or installation permission is introduced.

Validation: core preference/navigation and maintenance tests, WPF font-resource/license checks, font preview/discard, Back routing in both layouts, styled text selection/scrolling, theme and sidebar lifecycle checks, offline MSI payload hashes and launcher integrity checks. Light/Dark, bundled font variants, compact desktop and sidebar previews were reviewed. No real maintenance, font installation, application installation/upgrade/uninstall or runtime installation was performed. Broad Windows beta testing, keyboard/assistive-technology and live High Contrast transitions still require supervised validation. Existing sidebar live appbar/full-screen/mixed-DPI limitations remain documented.

Download RMPCS.Setup.exe for guided installation or use the standalone MSI when the runtime is already available. Portable ZIP, full source bundle, icons, runtime links and SHA256SUMS.txt are included. Exit RMPCS from the tray before upgrading. Discord announces the release after all required downloads are uploaded.

Unsigned development prerelease.
