# AI Assistant Analytics Events

### UI Trigger Backend Event

| SubType                        | Description                                                                                         |
|--------------------------------|-----------------------------------------------------------------------------------------------------|
| `favorite_conversation`        | Marks a conversation as favorite or not. Includes ConversationId, IsFavorite and ConversationTitle. |
| `delete_conversation`          | Deletes a previous conversation. Includes ConversationId and ConversationTitle.                     |
| `rename_conversation`          | Renames a conversation. Includes ConversationId and ConversationTitle.                              |
| `load_conversation`            | Loads a previously conversation. Includes ConversationId and ConversationTitle.                     |
| `cancel_request`               | Cancels a message request. Includes ConversationId.                                                 |
| `edit_code`                    | User edited the run command script.                                                                 |
| `create_new_conversation`      | User started a new conversation.                                                                    |
| `refresh_inspirational_prompt` | User refreshed inspirational prompt.                                                                |

---

### Context Events

| SubType                                    | Description                                                                                  |
|--------------------------------------------|----------------------------------------------------------------------------------------------|
| `expand_context`                           | User expanded the attached context section.                                                                                              |
| `expand_command_logic`                     | User expanded the command logic section.                                                                                                 |
| `ping_attached_context_object_from_flyout` | User pinged a context object from the flyout. Includes MessageId, ConversationId, ContextType and ContextContent.                        |
| `clear_all_attached_context`               | Cleared all attached context items. Includes MessageId and ConversationId.                                                               |
| `remove_single_attached_context`           | Removed a single attached context item. Includes MessageId, ConversationId, ContextType and ContextContent.                              |
| `drag_drop_attached_context`               | Dragged and dropped a context object. Includes MessageId, ConversationId, ContextType, ContextContent and IsSuccessful.                  |
| `drag_drop_image_file_attached_context`    | Dragged and dropped an image file into context. Includes MessageId, ConversationId, ContextContent (filename) and ContextType (file extension). IsSuccessful is always "true". |
| `choose_context_from_flyout`               | User chose a context object from the flyout. Includes MessageId, ConversationId, ContextType and ContextContent.                         |
| `screenshot_attached_context`              | User attached a screenshot via the screenshot button. Includes MessageId, ConversationId, ContextContent (display name) and ContextType ("Image"). |
| `annotation_attached_context`             | User attached an annotated screenshot via the annotation tool. Includes MessageId, ConversationId, ContextContent (display name) and ContextType ("Image"). Fired both when the initial screenshot is captured and when the annotated version is confirmed. |
| `upload_image_attached_context`            | User uploaded an image file via the file picker. Includes MessageId, ConversationId, ContextContent (display name) and ContextType (file extension). |
| `clipboard_image_attached_context`         | User pasted an image from the clipboard. Includes MessageId, ConversationId and ContextType ("Image"). ContextContent is null.            |

---

### Plugin Events

| SubType       | Description                                  |
|---------------|----------------------------------------------|
| `call_plugin` | User invoked a plugin. Includes PluginLabel. |

---

### UI Trigger Local Event

| SubType                                         | Description                                                                                                   |
|-------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| `open_shortcuts`                                | Opened the shortcuts panel.                                                                                   |
| `execute_run_command`                           | Ran a command from the UI. Includes MessageId, ConversationId and ResponseMessage                             |
| `use_inspirational_prompt`                      | User clicked an inspirational prompt. Includes UsedInspirationalPrompt.                                       |
| `choose_mode_from_shortcut`                     | User chose a shortcut mode. Includes ChosenMode.                                                              |
| `copy_code`                                     | User copied code from a run command response. Includes ConversationId and ResponseMessage.                    |
| `copy_response`                                 | User copied a response message from any command type. Includes ConversationId, MessageId and ResponseMessage. |
| `save_code`                                     | User saved a response message. Includes ResponseMessage.                                                      |
| `open_reference_url`                            | User clicked on a reference URL. Includes Url.                                                                |
| `modify_run_command_preview_with_object_picker` | User modified a run-command preview parameter via the object picker. Includes PreviewParameter.               |
| `modify_run_command_preview_value`              | User modified a run-command preview parameter value directly. Includes PreviewParameter.                      |
| `permission_requested`                          | A permission dialog was displayed to the user. Includes ConversationId, FunctionId and PermissionType. Fired when the dialog appears, before the user responds. |
| `permission_response`                           | User responded to a permission request. Includes ConversationId, FunctionId, UserAnswer and PermissionType.   |
| `window_closed`                                 | User closed the AI Assistant window. Includes ConversationId and IsAIRunning ("True" or "False"). |
| `window_opened`                                 | User opened the AI Assistant window. No extra fields.                                                         |
| `permission_setting_changed`                    | User changed a permission policy setting in the settings window. PermissionType contains the setting name; UserAnswer contains the new policy value. |
| `auto_run_setting_changed`                      | User toggled the Auto-Run setting. UserAnswer contains the new value ("True" or "False").                     |
| `new_chat_suggestions_shown`                    | The new-conversation screen with suggestion prompts became visible. No extra fields.                          |
| `suggestion_category_selected`                  | User clicked a suggestion category chip. Includes SuggestionCategory (e.g. "Troubleshoot", "Explore").       |
| `suggestion_prompt_selected`                    | User clicked a specific suggestion prompt. Includes SuggestionCategory and UsedInspirationalPrompt (the prompt text). |
| `mode_switched`                                 | User explicitly switched mode via the dropdown. Includes ChosenMode (e.g. "Agent", "Ask", "Plan") and ConversationId. |
| `plan_review_approved`                          | User approved the implementation plan. Includes ConversationId and ResponseMessage (plan file path).                  |
| `plan_review_feedback_sent`                     | User sent feedback on the implementation plan. Includes ConversationId, ResponseMessage (plan file path) and UserAnswer (feedback text). |
| `plan_review_cancelled`                         | User cancelled the plan review without approving or sending feedback. Includes ConversationId and ResponseMessage (plan file path). |
| `clarifying_question_submitted`                 | User submitted answers to the clarifying questions dialog. Includes ConversationId and ResponseMessage (total question count). |
| `clarifying_question_cancelled`                 | User dismissed the clarifying questions dialog without submitting. Includes ConversationId and ResponseMessage (total question count). |
| `retry_relay_connection`                        | User clicked the reconnect link after the relay connection was lost. Includes ConversationId.                 |
| `conversation_restored`                         | A conversation was restored after window reopen. Includes ConversationId and DurationMs (time to restore in milliseconds). |
| `expand_user_message_context`                   | User expanded the Context accordion on a sent user message. Includes ConversationId, MessageId and ContextItemCount. |
| `toggle_chat_history`                           | User clicked the Chats button to toggle the history panel. ActionValue is "True" (opened) or "False" (closed). |
| `scroll_to_bottom`                              | User clicked the More button to scroll to the bottom of the conversation. No extra fields.                   |
| `switch_run_command_tab`                        | User switched between Code and Output tabs in a Run Command result. Includes ConversationId and ActionValue ("Code" or "Output"). |
| `expand_function_call_result`                   | User expanded or collapsed a function call result details section. Includes ConversationId, FunctionId (tool name) and ActionValue ("True" = expanded, "False" = collapsed). |
| `open_assistant_settings`                       | User clicked "Open Assistant Settings" from the settings popup. No extra fields.                              |
| `collapse_reasoning_setting_changed`            | User toggled the "Collapse Reasoning when complete" setting. ActionValue contains the new value ("True" or "False"). ChosenMode contains the active mode ("Ask" or "Agent"). |
| `generate_content`                              | User clicked the Generate button in a generator. GeneratorType contains the generator type ("Animate", "Material", "Sound", "Mesh", "Texture", "Sprite", or "Cubemap"). Prompt contains the prompt text. NegativePrompt contains the negative prompt text. |
| `change_generator_mode`                         | User switched tabs in the Image generator. ActionValue contains the selected tab label (e.g. "Generate", "Remove BG", "Spritesheet", "Upscale", "Pixelate", "Recolor", "Inpaint"). |
| `error_displayed`                               | A technical error message became visible to the user. ErrorType discriminates the surface (`relay_stopped`, `server_incompatible`, `acp_session_error`, `acp_credential_error`, `acp_provider_unavailable`, `chat_error_block`). Includes ConversationId when available. ResponseMessage carries the underlying error text for surfaces that produce variable copy (`chat_error_block` today). Fires once per show transition, not per render. |
| `opened_via_integration`                        | The Assistant window was opened programmatically via a known integration and the user submitted a prompt. ActionValue identifies the caller (e.g. "CpuProfiler", "ProjectAuditor"). Prompt contains the user-submitted prompt text. |
| `ai_dropdown_opened`                            | User clicked the AI toolbar button, opening the dropdown. Fired on both the legacy (pre-6000.3) and modern toolbar paths. No extra fields. |
| `ai_install_accepted`                           | User clicked "Agree and install Unity AI" in the AI dropdown. Triggered by the Engine only (pre-install, package not yet present). No extra fields. |
<!-- EARLY_ACCESS_PROMO_START -->
| `desktop_early_access_signup`                   | User submitted the early-access promo banner. UserAnswer = email; ResponseMessage = `CloudProjectSettings.organizationKey`, recorded only when the user is signed in (`Account.sessionStatus.IsUsable`) — empty string otherwise; ActionValue = banner source. Ungated. |
<!-- EARLY_ACCESS_PROMO_END -->

---
---

## Field Schema Details

### Common Fields

| Field Name | Type   | Description                                             |
|------------|--------|---------------------------------------------------------|
| `SubType`  | string | Describes the specific type of action within the group. |

---

### UITriggerBackendEventData

| Field Name          | Type   | Description                                           |
|---------------------|--------|-------------------------------------------------------|
| `SubType`           | string | Specific subtype like 'cancel_request', etc.          |
| `ConversationId`    | string | ID of the conversation where the event occurred.      |
| `MessageId`         | string | ID of the message involved in the event.              |
| `ResponseMessage`   | string | The actual message text (if applicable).              |
| `ConversationTitle` | string | Title of the conversation.                            |
| `IsFavorite`        | string | Indicates if the conversation was marked as favorite. |

---

### ContextEventData

| Field Name       | Type   | Description                                     |
|------------------|--------|-------------------------------------------------|
| `SubType`        | string | Subtype like 'drag_drop_attached_context', etc.                                                       |
| `ContextContent` | string | Name or content of the context object. Null for `clipboard_image_attached_context` and `clear_all_attached_context`. |
| `ContextType`    | string | Type of the context object (e.g. type name, "Image", "LogData", file extension). Null for `clear_all_attached_context`. |
| `IsSuccessful`   | string | Whether the context interaction succeeded. Set for `drag_drop_attached_context` and `drag_drop_image_file_attached_context` only. |
| `MessageId`      | string | ID of the pending message this context was attached to.                                               |
| `ConversationId` | string | ID of the conversation this context was attached to.                                                  |

---

### PluginEventData

| Field Name    | Type   | Description                                 |
|---------------|--------|---------------------------------------------|
| `SubType`     | string | Subtype such as 'call_plugin', etc.         |
| `PluginLabel` | string | Identifier or label for the plugin invoked. |

---

### UITriggerLocalEventData

| Field Name                | Type   | Description                                            |
|---------------------------|--------|--------------------------------------------------------|
| `SubType`                 | string | Subtype such as 'open_shortcuts', etc.                 |
| `UsedInspirationalPrompt` | string | Inspirational prompt being used.                       |
| `SuggestionCategory`      | string | The suggestion category chip label (e.g. "Troubleshoot", "Explore"). Set for `suggestion_category_selected` and `suggestion_prompt_selected`. |
| `ChosenMode`              | string | Selected mode, such as run or ask mode.                |
| `ReferenceUrl`            | string | URL of the reference being called.                     |
| `ConversationId`          | string | ID of the conversation where the event occurred.       |
| `MessageId`               | string | ID of the message involved in the event.               |
| `ResponseMessage`         | string | Message content involved in the UI action. <!-- EARLY_ACCESS_PROMO_START -->For `desktop_early_access_signup`: repurposed to carry the active organization key from `CloudProjectSettings.organizationKey`, and only when the user is signed in (`Account.sessionStatus.IsUsable`); empty string otherwise, to avoid attaching a stale project-level org id to signed-out submissions.<!-- EARLY_ACCESS_PROMO_END --> |
| `PreviewParameter`        | string | Run command preview parameter after user modification. |
| `FunctionId`              | string | ID of the function/tool that requested the permission. |
| `UserAnswer`              | string | User's response or new setting value. For `permission_response`: "AllowOnce", "AllowAlways", or "DenyOnce". For `permission_setting_changed`: the new policy value. For `auto_run_setting_changed`: "True" or "False". <!-- EARLY_ACCESS_PROMO_START -->For `desktop_early_access_signup`: the email address entered by the user.<!-- EARLY_ACCESS_PROMO_END --> |
| `PermissionType`          | string | For `permission_requested`/`permission_response`: category of permission (e.g. "ToolExecution", "FileSystem", "CodeExecution", etc.). For `permission_setting_changed`: the name of the setting that changed. |
| `DurationMs`              | long   | Duration in milliseconds. Set for `conversation_restored` (time to restore the conversation). |
| `ContextItemCount`        | string | Number of context items attached to the user message. Set for `expand_user_message_context`. |
| `GeneratorType`           | string | The generator type: "Animate", "Material", "Sound", "Mesh", "Texture", "Sprite", or "Cubemap". Set for `generate_content`. |
| `NegativePrompt`          | string | The user's negative prompt text. Set for `generate_content`. |
| `IsAIRunning`             | string | Whether the AI was actively processing a response ("True" or "False"). Set for `window_closed`. |
| `Prompt`         | string | The user's prompt text. Set for `generate_content` (prompt for the generator) and `opened_via_integration` (the prompt the user submitted after the popup). |
| `ActionValue`             | string | The value associated with the user's action (e.g. selected tab, toggle state). Used by `toggle_chat_history`, `switch_run_command_tab`, `expand_function_call_result`, `collapse_reasoning_setting_changed`, `change_generator_mode`, `opened_via_integration` (integration name, e.g. "CpuProfiler", "ProjectAuditor")<!-- EARLY_ACCESS_PROMO_START -->, `desktop_early_access_signup` (banner source identifier, e.g. "empty_state_promo_banner")<!-- EARLY_ACCESS_PROMO_END -->. |
| `ErrorType`               | string | Discriminator for the error surface that was displayed. Set for `error_displayed`. Values: `relay_stopped`, `server_incompatible`, `acp_session_error`, `acp_credential_error`, `acp_provider_unavailable`, `chat_error_block`. |

---

### SendUserMessageEventData

| Field Name       | Type   | Description                                                                                         |
|------------------|--------|-----------------------------------------------------------------------------------------------------|
| `userPrompt`     | string | The user-entered prompt that triggered the event.                                                   |
| `commandMode`    | string | The active assistant mode when the message was sent: `"Agent"`, `"Ask"`, or `"Plan"`. Empty when mode is undefined. |
| `autoRunEnabled` | string | Whether Auto-Run is enabled. `"True"` or `"False"` when in Agent mode; null (omitted) otherwise.    |
| `conversationId` | string | Associated conversation ID.                                                                         |
| `messageId`      | string | ID of the user message being sent.                                                                  |

---

### User Message TTFT Event (`AIAssistantUserMessageTtftEvent`)

Tracks client-side Time To First Chunk (TTFT) for the regular Muse Chat (AIA) pipeline: the elapsed milliseconds between dispatching the user prompt to the backend and receiving the first response fragment. Complements the server-side TTFT metric (orchestrator pickup -> first token) by isolating client, network, and relay overhead from server processing time.

Fired exactly once per turn, when the first response fragment arrives. Not fired on the resume-after-domain-reload path (no preceding send to time against), and not fired for programmatic `SendChatRequest` callers (e.g. `ProjectOverview`, `AssistantApi`) which do not go through the user-driven prompt pipeline.

This event bypasses the sign-in gate used by other analytics so it is delivered for non-logged-in users as well.

#### UserMessageTtftEventData

| Field Name       | Type   | Description                                                                                  |
|------------------|--------|----------------------------------------------------------------------------------------------|
| `conversationId` | string | Associated conversation ID. Null if the conversation ID is not yet valid.                    |
| `messageId`      | string | ID of the assistant message that received the first fragment.                                |
| `ttftMs`         | long   | Elapsed milliseconds from prompt dispatch to first fragment arrival.                         |

---

## MCP Events

### MCP Session Event (`AIAssistantMcpSessionEvent`)

Tracks MCP session lifecycle — connection, client identification, and disconnection.

| SubType              | Description                                                                                                          |
|----------------------|----------------------------------------------------------------------------------------------------------------------|
| `SessionStart`       | Fired when a client connects and completes the handshake. Includes SessionId, SimultaneousConnections, and SimultaneousDirectConnections. |
| `ClientInfoReceived` | Fired when the client sends `set_client_info`. Includes SessionId, ClientName, ClientVersion, and OldClientName.     |
| `SessionEnd`         | Fired when a client disconnects. Includes SessionId, ClientName, SessionDurationMs, TotalToolCalls, LastToolName, and TimeToFirstSuccessMs. |

#### McpSessionEventData

| Field Name           | Type   | Description                                                                            |
|----------------------|--------|----------------------------------------------------------------------------------------|
| `SubType`            | string | Session event subtype: "SessionStart", "ClientInfoReceived", or "SessionEnd".          |
| `ClientName`         | string | Client name from `set_client_info` (e.g. "claude-code", "cursor").                    |
| `ClientVersion`      | string | Client version string.                                                                 |
| `SessionId`          | string | Connection identity key for correlating events within a session.                       |
| `SessionDurationMs`  | string | Total session duration in milliseconds. SessionEnd only.                               |
| `TotalToolCalls`     | string | Number of tool calls made during the session. SessionEnd only.                         |
| `LastToolName`       | string | Name of the last tool called before disconnect. SessionEnd only.                       |
| `TimeToFirstSuccessMs` | string | Time from session start to first successful tool call in milliseconds. SessionEnd only; empty if no successful calls. |
| `OldClientName`      | string | Previous client name when the client changes identity. ClientInfoReceived only.        |
| `SimultaneousConnections` | string | Total number of MCP transport connections active at the moment this session was registered. SessionStart only. |
| `SimultaneousDirectConnections` | string | Number of direct (non-gateway) MCP transport connections at session start. SessionStart only. |

---

### MCP Tool Call Event (`AIAssistantMcpToolCallEvent`)

Tracks individual MCP tool call completions with latency and error details.

| SubType             | Description                                                                                   |
|---------------------|-----------------------------------------------------------------------------------------------|
| `ToolCallCompleted` | Fired after each tool execution. Includes SessionId, ClientName, ToolName, Status, LatencyMs. |

#### McpToolCallEventData

| Field Name     | Type   | Description                                                             |
|----------------|--------|-------------------------------------------------------------------------|
| `SubType`      | string | Always "ToolCallCompleted".                                             |
| `ClientName`   | string | Client name from `set_client_info`.                                     |
| `ToolName`     | string | The MCP tool name that was executed (e.g. "Unity_ManageScene").         |
| `Status`       | string | "Success" or "Error".                                                   |
| `ErrorType`    | string | Exception type name on failure (e.g. "ArgumentException"). Empty on success. |
| `ErrorMessage` | string | Error message on failure. Empty on success.                             |
| `LatencyMs`    | string | Execution time in milliseconds.                                         |
| `SessionId`    | string | Connection identity key to correlate with session events.               |

---

## Gateway Events

### Gateway Event (`AIAssistantGatewayEvent`)

A single BigQuery event reused for several client-side quality signals, distinguished by `SubType`. Sent via EditorAnalytics. `TurnCompleted` is also mirrored to the gateway backend over HTTP (see below); the other subtypes are EditorAnalytics-only.

For every subtype except `TurnCompleted`, the structured payload is packed as a JSON string into the already-registered `Messages` field — this lets new client-quality metrics land in BigQuery with **zero schema registration** (the user cannot register new typed fields yet). Downstream readers parse `Messages` as JSON per the `SubType`.

| SubType            | Description                                                                                                | `Messages` payload |
|--------------------|------------------------------------------------------------------------------------------------------------|--------------------|
| `TurnCompleted`    | Fired when an ACP agent turn completes. Includes conversation metadata and full message history.           | message history (see below) |
| `TurnEnded`        | Fired once per classic-chat turn on every terminal state (completed / cancelled / error). Client-truth turn outcome for the Open-Beta P0 turn-completion and system-failure-rate metrics. | `{ outcome, failure_reason }` |
| `CompileResult`    | Fired after an agent code edit compiles (C# or shader). Client-truth for the "compile pass after change" P0. | `{ result, file_type, error_count }` |
| `GenerationResult` | Fired when an agent asset-generation tool reaches a terminal state (completed / failed). Client-truth for the asset-generation success/fail P0. | `{ asset_type, result, failure_reason }` |
| `StreamRecovery`   | Fired on the domain-reload incomplete-message replay path (the only place AIA recovers an interrupted stream). Feeds the redefined stream-disconnect/recovery P0. | `{ phase, result }` |

#### GatewayEventData

| Field Name       | Type   | Description                                                                                      |
|------------------|--------|--------------------------------------------------------------------------------------------------|
| `SubType`        | string | One of the subtypes above.                                                                       |
| `ConversationId` | string | The agent session ID. Set for `TurnCompleted` / `TurnEnded`; empty for the others.               |
| `Provider`       | string | The provider ID (e.g. "claude", "gemini"). `TurnCompleted` only.                                 |
| `TurnCount`      | int    | Number of turns in this conversation so far. `TurnCompleted` only.                               |
| `StartedAt`      | long   | Unix ms when the first prompt of the conversation was sent. `TurnCompleted` only.                |
| `EndedAt`        | long   | Unix ms when this turn's response completed. `TurnCompleted` only.                               |
| `Messages`       | string | For `TurnCompleted`: messages added since the last send, as a JSON string. Typically 2 messages (user prompt + assistant response), but may include more if intermediate turns were skipped by the 5-minute debounce; each element has `role`, `isComplete`, `timestamp`, and `blocks` fields, and the backend reconstructs the full conversation by joining all events for a `ConversationId` ordered by `EndedAt`. For the other subtypes: the JSON payload shown in the table above. |

##### `TurnEnded` payload (`Messages` JSON)

| Field            | Type   | Values                                                                                             |
|------------------|--------|----------------------------------------------------------------------------------------------------|
| `outcome`        | string | `completed`, `cancelled`, `error`, or `session_ended` (server graceful shutdown or informational/maintenance disconnect — a distinct terminal that is neither success nor failure; excluded from completion and failure rates, like CoCreate's `session-ended`). |
| `failure_reason` | string | On `error`: `auth`, `connection_lost`, `server_disconnect`, `no_capacity`, `client_parse_error`, or `unknown`. Null otherwise. |

##### `CompileResult` payload (`Messages` JSON)

| Field         | Type   | Values                                    |
|---------------|--------|-------------------------------------------|
| `result`      | string | `pass` or `fail`.                         |
| `file_type`   | string | `csharp` or `shader`.                     |
| `error_count` | int    | Number of compile errors on failure.      |

##### `GenerationResult` payload (`Messages` JSON)

| Field            | Type   | Values                                                                                     |
|------------------|--------|--------------------------------------------------------------------------------------------|
| `asset_type`     | string | The generated asset type (e.g. `Image`, `Material`, `Sound`, `HumanoidAnimation`, `Mesh`). |
| `result`         | string | `completed` or `failed`.                                                                   |
| `failure_reason` | string | On failure: `timeout`, `cancelled`, `invalid_args`, `asset_busy` (a generation was blocked because another asset is still generating / has interrupted downloads — a precondition, not a generation-system failure), or `generation_failed`. Null on success. Background (async hand-off) generations that complete later are not captured. |

##### `StreamRecovery` payload (`Messages` JSON)

| Field    | Type   | Values                                                                                          |
|----------|--------|-------------------------------------------------------------------------------------------------|
| `phase`  | string | `requested` (a recovery attempt started — volume only), `initiated` (replay successfully kicked off), or `failed` (recovery failed before replay). |
| `result` | string | Reserved for future detail; currently null.                                                     |

> Recovery success rate is computed terminal-only: `initiated / (initiated + failed)`. Do not divide by `requested` — early no-op bail-outs (nothing to recover) emit `requested` with no terminal.

---

### Gateway Analytics HTTP Event (`/v1/assistant/ai-gateway-analytics`)

Sent via relay bus POST (subject to a 5-minute debounce). A final flush is always sent when the session ends. The payload wraps an array of events under the `ai_gateway_events` key. This is a parallel path to the EditorAnalytics event above — both fire on the same trigger.

#### GatewayAnalyticsEvent

| Field Name         | Type    | Description                                                                                 |
|--------------------|---------|---------------------------------------------------------------------------------------------|
| `conversation_id`  | string  | The agent session ID (identifies the conversation on the backend).                          |
| `provider`         | string  | The provider ID (e.g. "claude", "gemini").                                                  |
| `turn_count`       | integer | Number of turns in this conversation so far (increments each time a prompt is submitted).   |
| `started_at`       | long    | Unix timestamp in milliseconds when the first prompt of the conversation was sent.          |
| `ended_at`         | long    | Unix timestamp in milliseconds when this turn's response completed.                         |
| `messages`         | array   | Messages added since the last send for this conversation. Typically 2 messages (user prompt + assistant response from the current turn), but may include more if intermediate turns were skipped by the 5-minute debounce. Each element is a message object with `role`, `isComplete`, `timestamp`, and `blocks` fields (see `AssistantMessage.ToJson()`). The backend reconstructs the full conversation by joining all events for a `conversation_id` ordered by `ended_at`. |

---

### Gateway TTFT Event (`AIAssistantGatewayTtftEvent`)

Tracks client-side Time To First Chunk (TTFT) for the Gateway (ACP) pipeline: the elapsed milliseconds between dispatching the user prompt to the agent session and the arrival of the first signal of progress for the turn (text, thought, tool call, or plan; captured inside `AcpProvider.EnsureAssistantMessage()`). Complements the server-side TTFT metric (orchestrator pickup to first token) by isolating client, network, and relay overhead from server processing time.

Fired exactly once per turn at the moment TTFT is computed, decoupled from the debounced batched `AIAssistantGatewayEvent` so the value cannot be overwritten by a later turn's reset. If the user cancels or an error occurs before any chunk arrives, no event is fired for that turn.

This event bypasses the sign-in gate used by other analytics so it is delivered for non-logged-in users as well. Sent only via EditorAnalytics (BigQuery); there is no parallel HTTP POST for this event.

#### GatewayTtftEventData

| Field Name           | Type   | Description                                                                                  |
|----------------------|--------|----------------------------------------------------------------------------------------------|
| `ConversationId`     | string | The agent session ID (identifies the conversation on the backend).                           |
| `Provider`           | string | The provider ID (e.g. "claude", "gemini").                                                   |
| `TurnCount`          | int    | The turn within the conversation that this TTFT applies to.                                  |
| `TtftMs`             | long   | Elapsed milliseconds from prompt dispatch to the first chunk arrival for this turn.          |
