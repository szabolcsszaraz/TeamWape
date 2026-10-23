# Changelog

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-09-28

### Added
- The Multiplayer Center window now opens automatically after the package is added to a project.

## [2.0.1] - 2026-07-03

### Changed
- Replaced the quickstart placeholder icon with a themed preview image.

### Fixed
- Install buttons no longer display "Installing package(s)..." after the user cancels the install dialog.
- Scrolling now works correctly when the cursor hovers over the Complexity bar.
- Help icon is now shown for locally embedded packages.
- Re-importing a sample now prompts the user to save the current scene before opening the example scene, preventing data loss.
- Package installation no longer blocks the Editor UI.
- Package and help icon buttons now correctly display hover and active states.

## [2.0.0] - 2026-02-25

### Changed
- The Multiplayer Center window has been reworked to focus on game genre selection with simplified options for users.
- `IOnboardingSection` and any related API is obsolete and will be removed in a future release.

### Fixed
- **Breaking change**: Moved the `Unity.Multiplayer.Center.Common` assembly to Editor only to avoid the forced dependency on UIElement in player builds.

## [1.0.1] - 2025-11-07

### Changed
- Removed references to Multiplayer Widgets, as the package has been deprecated.

## [1.0.0] - 2024-08-28

### Changed
- Become a core package

### Fixed
- Scoring of distributed authority hosting model in the Recommendations tab
- Minor wording and style improvement
- Packages that are not fundamentally incompatible with game specs are marked again as "Not Recommended" instead of "Incompatible"

## [1.0.0-pre.3] - 2024-08-21

### Fixed
- The Quickstart tab was not automatically shown after installing packages
- Made the UI look more responsive when reopening the window during package installation
- Added vertical Scrollbar to the Game specifications section

### Changed
- Always installing the latest Netcode for GameObjects 2.0.0-pre.x when it is selected as Netcode solution
- Made loading wheel more visible when installing packages (dark mode)
- Sort the game genre list alphabetically in the Recommendations tab

## [1.0.0-pre.2] - 2024-08-01

### Changed
- Widgets are now recommended for Netcode for Entities.

## [1.0.0-pre.1] - 2024-07-29

### Fixed
- Quickstart Tab: Show HelpBox if no quickstart package is installed but Quickstart Tab is visible. 

## [0.4.0] - 2024-07-19

### Added
- Added Recommendation for Distributed Authority hosting model with Netcode for GameObjects.
- Undo support for Recommendation tab.
- Added status Label at the bottom for package related information. 

### Changed
- Refined the Netcode Recommendation.
- The Quickstart tab is deactivated if no multiplayer-related package is installed.

## [0.3.0] - 2024-06-06

### Added
- Added analytics to the package

### Changed
- **New user interface for the recommendations tab**
- Multiplayer Center no longer upgrades packages that are embedded, linked locally, installed via Git or local Tarball
- Window can now be found under `Window > Multiplayer > Multiplayer Center`
- Updated the recommendation of the Widgets package (only compatible with Netcode for GameObjects at the moment)
- Updated the tooltips and various texts in the recommendations tab 

## [0.2.1] - 2024-04-25

Non user-facing changes only

## [0.2.0] - 2024-04-24

**Added**

- Automatic installation of the Getting Started content.
- Changed API for the getting started content.

## [0.1.0] - 2024-04-22

This is the first release of *com.unity.multiplayer.center*. The package provides a new Window available at `Window > Multiplayer Center` that gives a starting point to create a multiplayer game. 

The window consists of two tabs: `Recommendations` and `Getting Started`. 
- The `Recommendations` tab provides a customized list of packages and solutions to use based on the characteristics of your multiplayer game. 
- The `Getting Started` tab provides a list of resources based on the packages that you have installed.
