# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.20.0-pre.2] - 2026-10-02

### Fixed

- Fatal `BadImageFormatException: Bad IL format` crash on CoreCLR Editor builds (e.g. 7000.0) when creating or opening a project with the AI Assistant package (UUM-155231, UUM-149687).

## [2.20.0-pre.1] - 2026-09-17

### Changed

- Generated scripts that call APIs deprecated in the running Editor now report the deprecation and its replacement to the agent, including return-type changes, so it can converge on a fix (UUM-141875).
- Updated the What's New banner content for the 2.20 release.

### Fixed

- AI Assistant no longer freezes indefinitely when a domain reload is abandoned (e.g. an edit that fails to compile mid-turn); the relay now forces a reconnect after a bounded timeout so the editor recovers the in-flight turn.
- Disabled the model Change button and clarified its tooltip when no AI models are available.
- Generation feedback no longer reports every available category as selected; `feedbackCategories` now only carries categories the user actually chose (FUTR-2541).
- Opening or switching to a large conversation no longer freezes the Editor for seconds; the chat now virtualizes its content.
- Point cost labels now attach to the exact prompt a response answers (using the server's `correlation_message_id`) instead of the nearest preceding message, so a prompt re-sent mid-response no longer shows another turn's cost.
- The Assistant hanging on "Preparing..." in projects carrying a leftover `EXCLUDE_COMPILER_SERVICES_UNSAFE` scripting define.
- The relay adapter now follows the RelayService client swap on a proactive reconnect, so a closing message's completion is no longer orphaned when the connection is replaced mid-turn.
- The package now compiles on Unity 7000.0, where the Licensing API moved from `UnityEditor.Experimental.Licensing` to `UnityEditor.Licensing`.

## [2.19.0-pre.2] - 2026-08-31

### Changed

- Updated the What's New banner content for the 2.19 release.

### Fixed

- Completed conversations no longer falsely resume as "Preparing…" after a domain reload such as entering Play Mode (UUM-151091), and a recovery that cannot start now shows an error instead of spinning forever.

## [2.19.0-pre.1] - 2026-08-27

### Changed

- Credit and access banners read the org's AI subscription from the backend instead of inferring it from a refused request, and report a missing seat and a missing subscription together instead of one at a time.
- Credit and access buttons open the dashboard credits page for the billed org instead of a marketing page. Access banners label the button "Manage AI access" and explain the destination in a tooltip.

### Removed

- The "Start free trial" prompt. Trials are a billing action the dashboard owns and enforces.

### Fixed

- "Figma Settings" link in the Figma to UI example prompt was hard to read in Light Mode.
- "Update available" banner button now uses its intended lighter accent color in Light Mode.
- A prompt re-driven after a domain reload keeps the context that was attached to it, instead of being resent with no attachments.
- A prompt submitted just before a domain reload is re-driven after the reload instead of being lost.
- A tool call delivered twice while recovering from a domain reload now runs once, instead of executing the tool a second time without asking for permission again.
- A tool call redelivered after a domain reload now runs even when the chat session that requested it did not survive the reload, instead of being silently discarded and leaving the conversation to time out.
- Assistant no longer floods the conversation-list endpoint with duplicate requests during agent (ACP) sessions
- Assistant: a relay connection whose bus validation got no response (or that reached another editor's relay) is no longer treated as established; the attempt fails fast and the socket is disposed, preventing a reconnect wedge after domain reloads.
- Assistant: a turn cancelled by the server's stall watchdog now auto-continues on the same conversation (budgeted), instead of silently ending with an incomplete answer. The continuation is not started if the user cancelled the prompt.
- Assistant: incomplete-message recovery retries its replay request instead of giving up permanently when the relay connection is still re-establishing after a domain reload, and stops retrying once the recovery workflow can no longer receive the replay.
- Assistant: relay bus callers no longer throw a NullReferenceException after a connection attempt is rejected or the retries are exhausted — the transport is detached together with the client, so pending and later calls resolve as disconnected/cancelled.
- CS0618 compilation error on Unity 6000.7: MaterialGlobalIlluminationFlags.BakedEmissive renamed to BakedEmission.
- Feedback status text was light grey on a light background in Light Mode.
- Fixed contrast regression on Save icon from Trim feature.
- Fixed double ampersand for the View & Annotate label on MacOS.
- Fixed loading of non-ASCII skill resource files.
- Long-running tasks no longer end silently and appear complete when the session times out mid-turn; an interruption notice is shown instead
- MCP port collisions between concurrent editor relays — the relay now honors the FixedMcpPort assignment.
- Mid-turn connection loss now recovers automatically — a relay dead-peer watchdog surfaces silent half-open connections promptly, and the assistant auto-continues the interrupted turn (up to 3 attempts with escalating backoff). The pending re-drive survives a domain reload during its backoff, is dropped when a prompt starts or the user stops the turn, and only spends an attempt when a re-drive actually launches.
- Multi-line YAML frontmatter scalar in Skill description parsed as empty
- NullReferenceException in Chat Window
- Overlapping loads of the same conversation are now applied in request order, so a slower earlier load can no longer discard a recovered in-flight response from the chat view.
- Relay log now explains why a pinned relay or MCP port was not used when that port was excluded by an earlier selection or a failed start attempt.
- Session status banner dismiss link and background now use light-theme colors in Light Mode in all hosts.
- The assistant now loads the conversation when it is missing from the cache during post-domain-reload recovery, instead of abandoning the in-flight message.
- The relay now auto-delivers pending tool calls when the editor never requests message recovery after a reconnect, so in-flight turns survive instead of timing out.
- The relay now delivers a cloud disconnect that occurred during the editor's domain-reload window to the reconnected editor, so the turn auto-recovers instead of hanging until timeout.
- The toolbar dropdown now follows the same access rules and destination as the chat banner, and no longer resolves access before settings have loaded.

## [2.18.0-pre.1] - 2026-08-17

### Changed

- Updated AiEditorToolsSdk to 1.7.1: fixes domain-reload stalls during SDK response reads; adds HasAiSubscription/OptedInBillingOrgId settings signals and ToS-revert error propagation
- Updated the chat disclaimer to clarify that you're chatting with an AI assistant and responses may contain errors.

### Fixed

- "Read more" link in Preferences > AI > Assistant > Asset Knowledge is now legible in Light Mode
- Point cost labels now attach to the exact prompt a response answers (using the server's `correlation_message_id`) instead of the nearest preceding message, so a prompt re-sent mid-response no longer shows another turn's cost.
- AI Assistant now recovers reliably from cold-start relay delays and duplicate domain-reload requests.
- Assistant point balance refresh scheduling so the balance updates after message completion, cancellation, conversation errors, and session end.
- MCP connection failing on Windows accounts using CloudAP authentication (Microsoft Account / Windows Hello) — the named pipe security descriptor now reads the user SID from the process token instead of resolving it via LookupAccountName.
- Send permission policies to backend.
- Skill resource files (`references/*.md` and other bundled files) containing non-ASCII text — em dashes, curly quotes, arrows, accented characters, CJK or emoji — no longer fail to load with "does not appear to be a text file". Resource files now use the same UTF-8 validation as `SKILL.md`.
- Sound Generator trim mode — Stop and Loop now respond, and playback no longer overruns the trim boundary, when the Unity editor is unfocused.
- Sound Generator: spamming Save in Trim mode no longer throws `IOException` (sharing violation); the Save button is disabled while a save is in flight.
- Access-token refresh no longer opens the Unity Hub login flow in batch mode (the pre-injected token is used directly), falls back to the cached token instead of failing the session when the refresh times out, and never reports a successful refresh with an empty token.

## [2.17.0-pre.1] - 2026-07-28

### Added

- `CapabilitiesResponseV1.protocol_version` advertised as `1` so the backend can gate new server→client messages.

### Changed

- Credit and access banners now read the org's AI subscription from the backend instead of inferring it from a refused request, report a missing seat and a missing subscription together rather than one at a time, and open the dashboard credits page instead of a marketing page. The toolbar dropdown follows the same rules and destination as the chat banner.
- Replace "Unity AI" brand with descriptive language in user-facing UI strings.
- `ServerMessageJsonConverter` skips unknown server message `$type` with a warning log instead of throwing — improves forward-compatibility with newer backends.

### Fixed

- Domain reloads no longer stall while the SDK parses model-service responses.

## [2.16.0-pre.1] - 2026-07-21

### Changed

- MCP and gateway connections are no longer capped or gated by entitlement limits

### Fixed

- AI Assistant's ClientWebSocket fallback now routes through the configured HTTP/HTTPS proxy (honors HTTPS_PROXY/HTTP_PROXY and NO_PROXY)
- AI Generators now route their HTTP requests through the configured HTTP/HTTPS proxy (honors HTTPS_PROXY/HTTP_PROXY and NO_PROXY)

## [2.15.0-pre.2] - 2026-07-17

### Fixed

- Mid-turn domain reloads (e.g. the agent compiling a new script) no longer reset the Assistant to the landing page or lose the in-flight response when checkpoints are enabled.

## [2.15.0-pre.1] - 2026-07-16

### Added

- Analytics events for the EnterPlanMode approval prompt (approve / decline).
- Temporary Unity AI Desktop early-access promo banner.
- `Unity.EnterPlanMode` tool that prompts the user before switching the session into Plan mode.

### Fixed

- AI Assistant routes its relay and account connections through the configured HTTP/HTTPS proxy (respects HTTPS_PROXY/HTTP_PROXY and NO_PROXY).
- Prompt history (Up/Down) now recalls prompts on loaded conversations, navigates soft-wrapped multi-line entries by visual row, and no longer resets the caret at the oldest entry.

## [2.14.0-pre.1] - 2026-07-09

### Added

- Checkpoint discovery FTUE banner shown on first auto-initialization
- Profiler assistant can now read frame counter values, as a per-frame table or a min/max/median summary over a range of frames.
- Send the `conversation-kind: unity_editor` header on Assistant requests (WebSocket and REST) so backend metrics attribute in-editor traffic correctly.

### Changed

- Text-to-motion length is sent in the unit each model advertises (`x-unit` / `x-reference-frame-rate`) instead of assuming 30 fps; the duration slider stays in seconds. Models without these fields keep the previous behavior.
- `Unity.ExitPlanMode` tool now validates plan file paths before showing approval UI.

### Fixed

- A new-chat prompt is now recoverable when the server is at capacity — it routes to the capacity fallback (switch to the default model with an opt-in resend) instead of a generic error.
- AI Gateway analytics no longer send conversation content when Editor Analytics is disabled, including buffered content from turns taken while opted out.
- ArgumentNullException thrown when clicking Upscale or Generate PBR in the Material Generator without a prior generation.
- Conversations stuck in "thinking" state on abnormal connection close.
- EULA agreement button no longer hidden when the AI Assistant window is short.
- Empty code view when Assistant requests code execution.
- GenerateAsset tool now respects user-requested 3D mesh format (FBX/GLB) via new meshFormat parameter.
- IOException ("used by another process") when rapidly selecting a generated asset, e.g. double-clicking a generated Cubemap.
- Prevent WebSocket `1006` disconnects during long turns by absorbing the backend's idle keepalive frames in the relay instead of forwarding them to the Editor.
- Prompt typed on a new chat is no longer lost when the connection drops before the server responds — it (and any attached context) is restored to the input box with a retry.
- Upscale is no longer unavailable for valid cubemaps.
- `GenerateAsset` tool no longer hangs the chat turn indefinitely when generation download/post-processing stalls; the wait is now bounded by a timeout (5 min, 10 min for 3D/mesh) that falls back to an in-progress placeholder so the turn continues while the download finishes in the background.

## [2.13.0-pre.2] - 2026-06-25

### Fixed

- AI Assistant now recovers its connection automatically after system sleep or network interruptions, and stays responsive to tool calls while the Editor is unfocused

## [2.13.0-pre.1] - 2026-06-25

### Added

- New skills notification panel showing new local skill files in new chats

### Changed

- Changed RunCommand code execution, now driven using `MacroEvaluator`

### Fixed

- Assistant points balance now refreshes after conversations complete, error, or are cancelled.
- "Generate New" button in the Select Texture picker no longer overlaps the slider at minimum window width (UUM-132787)
- AI Assistant could fail to connect (TLS handshake failure / WebSocket 1015) on networks with TLS inspection or an enterprise root CA; the relay now trusts the OS certificate store.
- Checkpoints no longer report "Git LFS is not installed" on macOS when git-lfs is installed via Homebrew.
- Editor no longer crashes (out of memory) when `ManageGameObject get_components` serializes a GameObject whose components expose a non-TRS `Matrix4x4`.
- Fix unexpected connection revoked error.
- Gateway disclaimer heading no longer misaligned on first open under Bitmap text rendering mode (UUM-142565)
- Prevent an ArgumentOutOfRangeException when composing Korean/CJK IME text in the chat prompt field (including after undo).
- Register Matrix4x4Converter in the input serializer as well, keeping the input and output converter lists symmetric (UUM-144888).
- Returning from the checkpoint panel now restores the conversation scroll position (UUM-139446)
- Runtime assembly is no longer compiled into player builds unless `UNITY_AI_ASSISTANT_RUNTIME` is defined
- Stopped console warning spam during MCP connection validation when a foreign process's main module path cannot be read.

## [2.12.0-pre.2] - 2026-06-12

### Added

- Added explicit consent prompt before any asset generator-related task starts.

## [2.12.0-pre.1] - 2026-06-12

### Added

- Profiler assistant can now read frame counter values, as a per-frame table or a min/max/median summary over a range of frames.
- Prompt history: press Up/Down in the prompt field to step through previously submitted prompts in the current conversation.

### Fixed

- Escape no longer clears or restores the prompt field.
- Fixed ArgumentException when audio device changes during playback.
- Fixed Flux1 image models ignoring custom resolution.
- Gateway preferences now pre-fill the required environment variable names.

## [2.11.0-pre.2] - 2026-06-10

## [2.11.0-pre.1] - 2026-06-05

### Added

- MCP Streamable HTTP transport — configure remote MCP servers in `mcp.json` with `type: "http"`, `url`, and optional `headers`.
- XML documentation for the public API surface used by external agent tools.

### Changed

- Restoring a checkpoint now shows a confirmation dialog listing the files that will be affected (Added / Modified / Deleted) before proceeding.
- Users can now browse past conversations when out of credits.

### Fixed

- A failed checkpoint restore could leave the project in a partially-modified state. It now rolls back to the pre-restore HEAD if any step fails.
- Light mode issue on start page.
- Light mode issue with settings button in generator window.
- Remove false-positive "Summarizing..." status in chat.
- The chat window would stop updating partway through a word while the rest of the answer was already in the editor.
- Unity AI tag for meshes generated 3d assets.

### Security

- Checkpoint initialization now verifies that all project files were captured in the initial snapshot. If verification fails, checkpoints are disabled and the Settings view surfaces which files are missing along with Retry and "Initialize Anyway" options.

## [2.10.0-pre.1] - 2026-06-02

### Added

- Added UI for subagents.

### Fixed

- Fixed "Setting the parent of a transform which resides in a Prefab Asset is disabled to prevent data corruption" error.
- AI dropdown now shows the "Get More Credits" banner when the user has no allocated credits, matching the Assistant window behavior.
- Chat input limited to 4000 chars to prevent visual glitch where pasted text became invisible.
- Contrast adjusted for generators prompt boxes.
- Fixed a number of possible "Resolve of invalid GC handle" errors during domain reload or package installation.
- Model preview arrows no longer appear when hovering over models with no additional preview images in the AI model selector.
- AI toolbar button no longer disappears when the stale HideAIMenu EditorPref is set from a previous 6.2 project.

### Removed

- Removed gltFast as a hard dependency.

## [2.9.0-pre.2] - 2026-05-21

### Added

- Added no subscription banner.

### Fixed

- Fixed video-to-motion not working in Assistant.

## [2.9.0-pre.1] - 2026-05-20

### Added

- Client-side TTFT analytics events (`AIAssistantGatewayTtftEvent`, `AIAssistantUserMessageTtftEvent`) to measure prompt-to-first-chunk latency for Gateway and AIA pipelines.
- `Unity.EditPlan` tool for surgical revisions to existing plan files with snippet-diff display in the permission card.
- `View` button on permission prompts that opens the full action + raw input in the expanded panel.

### Changed

- Changed the What's New link.
- `Unity.ExitPlanMode` description references both `WritePlan` and `EditPlan` as preconditions.
- `Unity.WritePlan` and `Unity.EditPlan` now restricted to `Assets/Plans/` (was: anywhere in project).
- `Unity.WritePlan` description tightened to creation-only; revisions now route through `Unity.EditPlan`.

### Fixed

- Expanded panel title no longer squashed against the back button on non-PlanReview panels (CodeEdit etc.) due to a redundant `flex-grow: 0` rule.
- Gateway connection limit override set in developer tools now persists through domain reloads (Play Mode entry, script recompilation). Previously the override was silently lost on every reload.
- MCP bridge retry loop when host project has a broken global Newtonsoft JsonConverter (e.g. old com.unity.services.cloudsave).
- Permission prompt Allow/Deny buttons no longer get pushed off-screen when the tool call title is multi-line (e.g. Bash heredoc commands). Title/detail now stay on a single line; a new `View` button opens an expanded panel with the full content.
- Reduced generator skill over-triggering by adding explicit-intent and code-first guards to the `GenerateAsset` tool description.
- Skip button on the AskUser dialog stays visible after skipping the last question — now hidden once there's nothing left to skip.

## [2.8.0-pre.1] - 2026-05-14

### Added

- Added description in model selection window when no models are visible.
- Skills are scanned from UPM package `AIAssistantSkills` folder.

### Changed

- Add Revise plan flow to ExitPlanMode approval card.

### Fixed

- Hide the technical `AttachedImageReference` context entry from the conversation history UI so it no longer appears as a stray widget after domain reload.
- Implementation plan UI, expanded plan view, and elapsed-time counter no longer disappear or reset when a domain reload happens while the chat is awaiting plan approval.
- NullReferenceException in Chat Window.
- Pro license fallback now grants three MCP and AI Gateway connections.
- Replace additional placeholder .meta file GUIDs that could collide with user projects.
- Users without an active Unity AI subscription now see the trial banner instead of the "Get More Credits" banner across the AI dropdown, Generators, and Assistant.
- `SKILL.md` files authored in CJK languages (Chinese, Japanese, Korean) now load correctly.
- Fixed Open Package Manager link not working.
- Fixed Update button not giving feedback when updating Assistant.

## [2.7.0-pre.3] - 2026-05-06

### Added

- Added the Voice tooltip to Generation Data.
- Surface Unity entitlement-derived connection limits (AllowedGatewayConnections, AllowedMcpConnections) on SettingsState so UI can gate features by Personal / Pro / Enterprise tier.

### Fixed

- Fixed Assistant generating voices.
- Replace placeholder .meta file GUIDs that could collide with user projects.
- Local Unity license entitlements now contribute to MCP and AI Gateway connection limits.
- UNITY_PROJECT_PATH / --project-path targeting for the Unity MCP server now matches the project root as documented.

### Removed

- Removed the count on 3d generators and default to 1.

## [2.7.0-pre.2] - 2026-05-05

### Fixed

- Fixed Video to Motion.
- Fixed Search overflow in Assistant window.
- Fixed Convert reference model from FBX to GLB when rigging model only accepts glTF input.
- Fixed light mode references button.

### Removed

- Removed the subscription link from the trial banner.

## [2.7.0-pre.1] - 2026-04-28

### Added

- Expanded view for chat elements.
- Figma Access Token configuration in Project Settings with API verification.
- Figma-to-UI agent tools for listing screens and importing design assets from Figma projects.
- Generic CredentialSet relay channel for storing and deleting credentials in the OS keychain.
- Plan mode can now read console logs and query package info when producing an implementation plan.
- Public AssistantApi surface (Run, PromptThenRun, RunHeadless, AttachedContext, VirtualAttachment, IAgent).
- RunReadOnlyCommand tool to plan mode.

### Changed

- "Beta" chip label on the New Chat screen.
- Add more fields to gateway and MCP analytics.
- Attachment view redesign.
- Feedback redesign.
- Redesign session status banners to show context-specific messaging and actions based on user license type and points status.
- Todo progress widget header renamed from "Executing Plan" to "Task List".

### Fixed

- Async fire-and-forget tasks could block the calling thread before the first await in WithExceptionLogging.
- Attachment drag-and-drop animation.
- Autorun state now tracked in analytics for plan mode.
- Autorun toggle now enabled in plan mode.
- Changed all user-facing mentions of "points" to "credits" in Assistant window and AI button.
- ChatElementBase is no longer redundantly re-added to the root VisualElement on every SetupChatElement call.
- CheckpointEnabled (and other Assistant preferences) no longer revert to defaults after restoring a checkpoint in projects that gitignore `ProjectSettings/Packages/`.
- Conversation no longer hangs on "Preparing..." after domain reloads triggered by parallel `CodeEdit` tool calls.
- Fix What's New banner clipping behind centered footer in empty state.
- Fixed soft-lock of graph creation on large projects.
- GetFileContent now enforces a 2 MB size limit and rejects binary files, preventing editor hangs when large or binary assets are read.
- Increase "Summarizing..." timeout from 3s to 6s to reduce false positives during streaming.
- NullReferenceException in Chat Window.
- NullReferenceException in RunCommandTool when agent script uses an unauthorized namespace (System.Reflection, System.Net, etc.); agent now receives a specific error message naming the blocked namespace.
- Reasoning blocks no longer visually interrupt continuous text in chat responses.
- Removed unnecessary empty space below the text in the Details block of the Unity_RunCommand tool in the Unity MCP Project Settings section.
- RunCommand tool on Unity 6.5+.
- Show "Awaiting input..." in the progress indicator when a tool is waiting for user response.
- The **Refresh Project Overview** button is now hidden for non-Unity (ACP) providers, where it had no effect.
- Todo panel collapsed state is now preserved across domain reloads and conversation switches.
- Undo/Redo keyboard shortcuts now work in prompt text fields.
- When adding context via the `+` button, the input field shouldn't add the file name.
- `BaseInteraction.CancelInteraction()` now fires `OnCompleted` so interaction queue entries are properly cleaned up on cancel.
- `BaseInteraction<T>` and `DialogToolUiContainer` no longer throw `InvalidOperationException` on double-cancel races, preventing the UI ghost-panel state.

### Security

- Prevent API keys and tokens from leaking into relay logs and trace files.

## [2.6.0-pre.1] - 2026-04-14

### Changed

- Moved Profiler Assistant to Skills implementation.
- Update Plan mode tool descriptions.

### Fixed

- "Check Status" button in Project Settings > Unity MCP Server now logs the integration status to the Console.
- Added tooltip to conversation context counter.
- Fixed a few icons that were not dark enough in light mode.
- GetConsoleLogs tool timestamps.
- MCP Server settings subtitle text overflow.
- NullReferenceException in Chat Window.
- Preferences search bar now finds matches on the Assistant tab.
- Some image types cannot be used as assistant attachments.
- Tool renderer header icons getting squashed when tool call title is too long.

## [2.5.0-pre.2] - 2026-04-09

### Fixed

- Fixed compilation error when com.unity.ai.inference package is installed on Unity 6000.5.

## [2.5.0-pre.1] - 2026-04-09

### Added

- Grep tool.
- Plan mode.
- Project Settings `AI / Skills` page to see and validate AI Assistant skills.
- Public API "AgentTool" to declare tools for the AI Assistant.
- Skill loading logic for AI Assistant's skills loaded from the filesystem.
- Support `required_editor_version` field in SKILL.md frontmatter for editor version gating.
- Support for Unity 6000.5 Beta.
- `ask_user` tool UI with multi-question navigation, single/multi-select layout, and yes/no support.

### Fixed

- AskUser tool returns a descriptive error to the model when the questions JSON is malformed.
- Clicking a regular option after selecting Other left two radio buttons appearing selected simultaneously.
- Feedback text field left edge misaligned.
- Fixed "Ask" route get reset to "Agent" at every domain reload.
- Fixed ArgumentNullException for Upscale and PBR tabs.
- Game data collection hook not attaching when Assistant window is opened after editor startup.
- Ignore codex MCP probe connections from settings UI to avoid jitter.
- Image previews from screenshot and GetImageAssetContent tools disappearing when reasoning section collapses.
- MCP Integration in project settings.
- MCP duplicate connections for identical clients.
- MCP tool calls such as RunCommand now properly update the asset database and wait for compilation prior to running.
- NullReferenceException in Chat Window.
- PendingInlineInteractions cleared on conversation switch to prevent memory leaks.
- Permission requests now always surface immediately when the todo panel is showing.
- Relay not being copied to `~/.unity/relay` reliably for MCP use.
- Shared relay install logic between RelayService and ServerInstaller to fix Mac relay installation.
- Single Completed subscription per content object prevents redundant callbacks on recycling.
- Todo updates scoped to originating conversation ID, preventing cross-conversation bleed.
- `ask_user` Other field text misaligned by a few px compared to regular option labels.
- ripgrep code signing.

### Removed

- Unity.FindFiles tool.

## [2.4.0-pre.1] - 2026-03-30

### Added

- Add feedback source tracking for generation feedback.
- Mesh generation pivot export settings.
- USS import error surfacing in CodeEditTool for .uss files.

### Changed

- Improved CodeEdit string matching logic for line ending and indentation mismatches between LLM output and file content.

### Fixed

- Compilation when "USE_ROSLYN" is added to Scripting Define Symbols.
- Fixed clipboard attachment inverted.
- Fixed context button staying highlighted.
- Fixed duplicate Assistant window appearing when maximizing/restoring editor windows.
- NullReferenceException in Chat Window.

## [2.3.0-pre.2] - 2026-03-23

### Fixed

- Fixed What's new banner not loading correctly.

## [2.3.0-pre.1] - 2026-03-23

### Added

- Added a standalone 2D scene capture tool and enhanced the Multi-Angle Scene View tool with object focusing capabilities for better visual validation.
- Added feedback text in generators.
- Client-side `Unity.Web.Fetch` tool for hybrid web search architecture with HTML-to-markdown conversion, Discourse JSON API support, and browser User-Agent for bot-detection bypass.
- Game-Data-collection package: README with onboarding guide for the game-data-collection package.
- The feature of GetDependency tool to query the dependency graph.
- Toggle in project settings for batch mode auto-approve for Unity MCP.

### Changed

- Game-Data-collection package: Snapshot commit messages now include the first 15 words of conversation content for easier identification.
- MCP connection speed to be much faster.

### Fixed

- Assistant tries and fails repeatedly to generate a sprite with a background color.
- Fixed banner not dismissing after package update from package manager.
- Invalid model selected in generator windows.
- NullReferenceException in Chat Window.
- Assistant issues related to editor not in focus.
- Brittle connection behaviour due to multiple tool updates during MCP server initialization phase.
- Connection issues related to editor not in focus.
- MCP connection issues in batch mode.
- MCP reconnection after domain reload.
- MCP tool calls failing during domain reload validation window.

## [2.2.0-pre.1] - 2026-03-16

### Added

- Added feedback system for Generators.
- LLM Model selection for AI Assistant.
- Added dependency graph creation and refreshing.

### Changed

- Changed feedback UI for Assistant.
- Moved Profiler Assistant to Skills implementation.

### Fixed

- Clear all attachments (objects, console logs, virtual attachments) after sending a prompt, not just screenshots.
- Cursor access fixed.
- NullReferenceException in Chat Window.
- Project image asset contents unable to be read by AI Assistant.

## [2.1.0-pre.1] - 2026-03-09

### Added

- Tool Unity.GetSceneInfo.

### Changed

- `GameObjectSerializer`, `ObjectsHelpers`  `Response` in `Unity.AI.MCP.Editor.Helpers` to `public`.
- Changed how annotations are sent to backend by sending a mask of the annotations.
- Removed SceneInfo, ProjectDependency, ProjectSetting, ProjectVersion from GetProjectData tool output.

### Fixed

- NullReferenceException in Chat Window.

## [2.0.0-pre.1] - 2026-03-03

### Added

- Made MCP tools and APIs public, including UI for MCP tool categories and documentation.
- Added Skills Developer Frontend.
- Added opacity slider annotation.
- Added logging for code edit issues.

### Changed

- Merged `com.unity.ai.toolkit` and `com.unity.ai.generators` packages into `com.unity.ai.assistant`.
- Reduced monopackage public API.
- Updated Assistant interface, integrations, and skill creation documentation.
- Added ability to dismiss low points banner.
- Added tags to identify SkillDefinition for removal in Skills Frontend.
- Removed icons from the provider selector.
- Improved log debugger.
- Used full install path to reference Cursor CLI for running login.

### Fixed

- Fixed Annotation Tool taking screenshot on same monitor as Unity on macOS.
- Fixed 99% stuck generations.
- Fixed asset generator thought element reuse.
- Fixed getting stuck after an answer.
- Fixed domain reload endless blocker.
- Fixed ACP client stuttering.
- Fixed MCP Client initialization bug.
- Fixed a regression with relay.
- Fixed missing generation result checks.
- Fixed cancellation issues where prompt cancellation also canceled title generation.
- Labeled canceled messages as complete.
- Fixed skill definition metadata and YAML parsing corrections.
- Fixed thought sequence hiding if tool is still in progress.
- Ensured thought chunks are rendered as thought blocks for ACP conversations.
- Fixed various asset generator bugs.
- Fixed issue with stale queue indices when switching conversations.

## [1.7.0-pre.1] - 2026-02-23

### Added

- Annotation Tool

### Fixed

- Fixed interrupted conversations in AI Gateway
- Fixed RunCommand failing
- Fixed permission issues from UI refactor
- Improved conversation recovery and resilience

## [1.6.0-pre.3] - 2026-02-16

### Added

- Added Checkpoints
- Added MCP Client

### Fixed

- Fixed Mac Relay Code signing

## [1.6.0-pre.2] - 2026-02-10

### Added

- Re-enabled Unity 6.0 support

### Changed

- Removed AI Gateway from settings pages

### Fixed

- Fixed reasoning collapse issue
- Fixed race condition in DispatchWithOverride causing truncated chat responses

## [1.6.0-pre.1] - 2026-02-09

### Added
- Added links to the Profiler Window for the profiling agent integration

## [1.5.0-pre.2] - 2026-01-15

### Added

- What's New section about points.

### Changed

- Changed package description.

### Fixed

- Fixed right-click issue in chat history.
- Fixed new missing permissions cost.

## [1.5.0-pre.1] - 2026-01-05
- Added image and screenshot attachments support with multiple format compatibility
- UI Builder Agent with a preview system
- Permissions system for file create/modify/delete actions with session overrides
- New Ask mode for question-focused interactions without executing actions
- Code Edit tools with diff and syntax highlighting
- Package Manager tools for add/remove/embed packages
- Project Overview tool for quick project structure summary
- New Orchestrator with improved multi-step workflows and automatic tool review
- Added reasoning blocks with auto-hide options

## [1.0.0-pre.12] - 2025-08-12
- Update AI-Toolkit to 1.0.0-pre.18

## [1.0.0-pre.11] - 2025-07-31
- Added tooltips to actions buttons
- Cannot press enter anymore to cancel chat progress
- Fuzzy matching improvements
- Added links to new docs
- Conversation is retained on domain reload
- Remove preview chips from routes

## [1.0.0-pre.10] - 2025-07-16
- Minor bug fixes
- Fixed issue where URLs were not being updated automatically for upgrading users
- Fixed issue where versions API endpoint reporting unsupported server versions was not shutting down the UI

## [1.0.0-pre.9] - 2025-07-15
- Return enter cancels prompt and resends it
- Fix code edit temp file
- Check for unauthorized code before loading the assembly
- Support function calling during streaming

## [1.0.0-pre.8] - 2025-06-27
- Moved Plugin Attributes back to AI Assistant

## [1.0.0-pre.7] - 2025-06-25
- Minor bug fixes
- Improved error handling for function calling
- Finalized onboarding content
- Disabled run when code won't compile
- Fixed performance issues for long conversations
- Improved history panel performance

## [1.0.0-pre.6] - 2025-06-05
- Bump AI Toolkit to 1.0.0-pre.12
- Fixed issue blocking .NET Standard Editor compatibility
- Minor UI fixes
- Minor bug fixes
- Improved attachment selection UX
- Fixed issue adding additional attached context to prompts
- Added support for array types as run parameter field.

## [1.0.0-pre.5] - 2025-05-14
- Improved Agent deletion logic
- Improved search window performance
- Improved automatic conversation naming
- Fixed issue with conversation history after editing agent actions
- Other small bug fixes

## [1.0.0-pre.4] - 2025-05-13
- Update AI-Toolkit to 1.0.0.-pre.7
- Added console tab attachments
- Updated to beta backend urls
- Fixed source attribution placement
- General usability fixes
- Fixed access token refresh issue
- Fixed bugs related to API alignment
- Assistant can now be used when the editor is paused

## [1.0.0-pre.3] - 2025-04-22

### Changed
- update AI-Toolkit to 1.0.0.-pre.4

## [1.0.0-pre.2] - 2025-04-16

### Changed
- Version Bump for re-release in production

## [1.0.0-pre.1] - 2025-04-11

### Changed
- Initial release of the AI Assistant Package
- Adds a menu item at `Window > AI > Assistant` to access tool
- Updated to interact with the new AI Assistant server using the AI Assistant protocol
