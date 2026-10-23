using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The scroll container the block-virtualized conversation lives in: a clipped viewport, a content
    /// layer that holds the absolutely positioned slots, and a standalone <see cref="Scroller"/> that is
    /// a readout only. The authoritative position is <see cref="Model"/>'s anchor, so neither a layout
    /// outcome nor a scrollbar write can move the viewport. Realizing items belongs to the realizer,
    /// which subscribes to <see cref="PassRequested"/> — that is what keeps this class free of every
    /// chat element type.
    /// </summary>
    class ChatScrollSurface : VisualElement
    {
        const string k_RootClass = "mui-chat-surface-root";
        const string k_ViewportClass = "mui-chat-surface-viewport";
        const string k_ContentClass = "mui-chat-surface-content";

        // Stock ScrollView scrolls by delta.y times --unity-metrics-single_line-height, 18 px by default.
        const float k_LineHeight = 18f;

        // One viewport minus a tenth of it, so a page keeps a sliver of context. Stock computes this in
        // content pixels, which at an 89,000 px conversation pages by a handful of pixels.
        const float k_PageOverlapRatio = 0.9f;

        const int k_MaxFirstPaintFrames = 4;
        const int k_NoPointer = -1;

        readonly ItemHeightModel m_Heights = new();
        readonly ChatScrollModel m_Model;

        IVisualElementScheduledItem m_Pump;
        bool m_PassRequested;
        bool m_LandAtEndPending;
        bool m_Undisplayed;
        bool m_WritingScroller;
        float m_LastScrollerValue;
        float m_LastTotalHeight = float.NaN;
        int m_FirstPaintPasses;
        long m_FirstPaintTicks;
        int m_FirstPaintItems;
        int m_DragPointerId = k_NoPointer;
        int m_ScrollerPointerId = k_NoPointer;

        public ChatScrollSurface()
        {
            m_Model = new ChatScrollModel(m_Heights);

            AddToClassList(k_RootClass);

            Viewport = new VisualElement { name = "chatSurfaceViewport" };
            Viewport.AddToClassList(k_ViewportClass);

            Content = new VisualElement { name = "chatSurfaceContent" };
            Content.AddToClassList(k_ContentClass);
            Viewport.Add(Content);

            VerticalScroller = new Scroller(0f, 0f, OnScrollerValueChanged, SliderDirection.Vertical);

            // The Scroller constructor gives its slider a viewDataKey; a restored value would be written
            // straight onto the slider's fields and drag the anchor to the last session's offset.
            VerticalScroller.slider.viewDataKey = null;

            Add(Viewport);
            Add(VerticalScroller);

            BeginFirstPaint();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>Clipped window onto the content layer. Every input handler lives here.</summary>
        public VisualElement Viewport { get; }

        /// <summary>Positioning space for the slots, and the box the 768 px column is measured in.</summary>
        public VisualElement Content { get; }

        public Scroller VerticalScroller { get; }

        public ChatScrollModel Model => m_Model;

        public ItemHeightModel Heights => m_Heights;

        /// <summary>Foldout expansion, keyed by identity so it outlives the element that renders it. The
        /// flattener reads it, which is what lets a collapsed container prune its subtree.</summary>
        public ChatViewStateStore ViewState { get; } = new();

        public float ViewportHeight
        {
            get
            {
                var height = Viewport.layout.height;
                return float.IsNaN(height) ? 0f : height;
            }
        }

        /// <summary>False until the viewport's items have been measured, so nothing is ever painted at
        /// a provisional position.</summary>
        public bool ContentRevealed { get; private set; }

        /// <summary>
        /// Ticks once per panel update the pump sees. The panel lays out between updates, so a later tick
        /// is the surface's evidence that a layout has run — and unlike <c>Time.frameCount</c>, which the
        /// idle Editor leaves frozen because the player loop is not pumped, it keeps advancing whenever
        /// the panel is alive.
        /// </summary>
        public long PumpTick { get; private set; }

        /// <summary>Raised at the top of a pass, before anything reads the display stream, so an owner
        /// whose stream is stale can re-flatten into it.</summary>
        public event Action PassStarting;

        /// <summary>Raised at most once a frame, when a pass was requested or the first paint is still
        /// pending. The realizer runs on it.</summary>
        public event Action PassRequested;

        /// <summary>Raised when the content layer is revealed, once per <see cref="BeginFirstPaint"/>.
        /// This is the conversation becoming visible, which is what a load is timed to.</summary>
        public event Action FirstPaintCompleted;

        public event Action UserScrolled;

        public event Action GeometryChanged;

        /// <summary>Raised when the display stream no longer describes the conversation, because a
        /// container was collapsed or reopened. The owner re-flattens and rebases the anchor.</summary>
        public event Action ReflattenRequested;

        public void RequestPass() => m_PassRequested = true;

        /// <summary>Records what a foldout was toggled to and asks the owner to re-flatten, because a
        /// collapsed container prunes its subtree from the display stream.</summary>
        public void SetExpanded(ContentKey identity, bool expanded)
        {
            ViewState.SetExpanded(identity, expanded);
            ReflattenRequested?.Invoke();
        }

        public void ScrollToEnd()
        {
            Follow(tail: true);
            m_Model.MoveToEnd(ViewportHeight);
            RequestPass();
        }

        /// <summary>Anchors the view on an item, <paramref name="offset"/> pixels into it. Leaving the
        /// tail is part of the move: a position someone named is not one the stream may drag away.</summary>
        public void ScrollTo(int item, float offset)
        {
            Follow(tail: false);
            m_Model.Set(item, offset, ViewportHeight);
            RequestPass();
        }

        public void ScrollBy(float pixels)
        {
            Follow(tail: false);
            m_Model.MoveBy(pixels, ViewportHeight);
            ReArmIfLandedAtTheBottom();
            RequestPass();
        }

        /// <summary>Puts the view back on a captured position, in the stream as it stands now. A position
        /// taken at the end returns to the end, wherever the conversation has grown to since.</summary>
        public void RestorePosition(ChatScrollPosition position, IReadOnlyList<DisplayItem> items)
        {
            Follow(position.AtEnd);
            m_Model.Restore(position, items);
            RequestPass();
        }

        /// <summary>
        /// Lands the view on the end once, and then leaves it to the reader. Unlike the tail follow this
        /// survives until a pass can act on it, so a caller may ask for it on a surface that has not been
        /// attached, laid out or painted yet — which is how a checkpoint snapshot is built.
        /// </summary>
        public void LandAtEndOnce()
        {
            m_Model.StickToBottom = false;
            m_LandAtEndPending = true;
            RequestPass();
        }

        /// <summary>
        /// Hides the content layer until the viewport is realized and measured again. Called when the
        /// conversation is replaced, so a reload never shows a staircase of items popping in.
        /// </summary>
        public void BeginFirstPaint()
        {
            ContentRevealed = false;
            m_FirstPaintPasses = 0;
            m_FirstPaintTicks = AssistantPerf.Now;
            Content.SetVisible(false);
            RequestPass();
        }

        /// <summary>
        /// Reveals the content layer once every item overlapping the viewport has a measured height.
        /// Called at the end of a realize pass with the range the realizer just positioned.
        /// </summary>
        public void TryReveal(ItemRange viewportRange)
        {
            if (ContentRevealed)
                return;

            m_FirstPaintItems = viewportRange.Count;

            for (var index = viewportRange.First; index < viewportRange.LastExclusive; index++)
            {
                if (!m_Heights.IsMeasured(index))
                    return;
            }

            Reveal();
        }

        /// <summary>
        /// Rewrites the scrollbar from the height model. Everything here is allowed to be wrong while
        /// items are unmeasured and to improve as they are measured: the value is a readout, and
        /// <see cref="OnScrollerValueChanged"/> discards whatever this write notifies, so it can never
        /// come back as a position change.
        /// </summary>
        public void UpdateScroller()
        {
            // While the knob is held the hand is the readout. Writing the value would move the knob out
            // from under the pointer, and writing the range or the knob's size would rescale the mapping
            // the drag is being computed against — the slider reads the gesture off its own geometry.
            if (IsScrollerHeld())
                return;

            var total = m_Model.EstimatedTotal;
            var viewport = ViewportHeight;

            m_WritingScroller = true;
            try
            {
                VerticalScroller.lowValue = 0f;
                VerticalScroller.highValue = Mathf.Max(0f, total - viewport);
                VerticalScroller.Adjust(total <= 0f ? 1f : Mathf.Clamp01(viewport / total));
                VerticalScroller.slider.pageSize = viewport * k_PageOverlapRatio;
                VerticalScroller.slider.SetValueWithoutNotify(m_Model.EstimatedOffset);

                // Read back rather than remembered: the slider clamps an offset the estimated range
                // cannot hold, and the next gesture is measured from the value the slider really holds.
                m_LastScrollerValue = VerticalScroller.value;
            }
            finally
            {
                m_WritingScroller = false;
            }

            // Hidden rather than undisplayed: the scroller keeps its width either way, and on a panel
            // narrower than the content's 768 px cap a changing width would drop every measurement.
            VerticalScroller.SetVisible(VerticalScroller.highValue > 0f);
        }

        /// <summary>Whether a pointer is holding any part of the scroller — the knob, the track or a
        /// repeat button. The slider captures the pointer for the whole gesture, and a pen or a touch
        /// drags the scrollbar exactly as a mouse does, so the held pointer is the one that pressed it.</summary>
        bool IsScrollerHeld()
        {
            var pointerId = m_ScrollerPointerId == k_NoPointer ? PointerId.mousePointerId : m_ScrollerPointerId;

            if (panel?.GetCapturingElement(pointerId) is not VisualElement holder)
                return false;

            return holder == VerticalScroller || VerticalScroller.Contains(holder);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Viewport.RegisterCallback<WheelEvent>(OnWheelBeforeDescendants, TrickleDown.TrickleDown);
            Viewport.RegisterCallback<WheelEvent>(OnWheel);
            Viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            Viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            Viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            Viewport.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            VerticalScroller.RegisterCallback<PointerDownEvent>(OnScrollerPointerDown);
            VerticalScroller.RegisterCallback<PointerUpEvent>(OnScrollerPointerUp);

            // EditorApplication.update throttles to about 100 ms when the Editor is idle; the panel
            // scheduler keeps running, which is why the pump lives here and not there.
            m_Pump ??= schedule.Execute(OnPump).Every(0);
            m_Pump.Resume();

            RequestPass();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Viewport.UnregisterCallback<WheelEvent>(OnWheelBeforeDescendants, TrickleDown.TrickleDown);
            Viewport.UnregisterCallback<WheelEvent>(OnWheel);
            Viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            Viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            Viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            Viewport.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            VerticalScroller.UnregisterCallback<PointerDownEvent>(OnScrollerPointerDown);
            VerticalScroller.UnregisterCallback<PointerUpEvent>(OnScrollerPointerUp);

            m_Pump?.Pause();
            m_PassRequested = false;
            m_DragPointerId = k_NoPointer;
            m_ScrollerPointerId = k_NoPointer;
        }

        void OnScrollerPointerDown(PointerDownEvent evt) => m_ScrollerPointerId = evt.pointerId;

        void OnScrollerPointerUp(PointerUpEvent evt)
        {
            if (m_ScrollerPointerId != evt.pointerId)
                return;

            m_ScrollerPointerId = k_NoPointer;

            // Where a scrub ends is the position the reader chose; a knob let go at the bottom is a
            // reader back on the newest message, and the last value change of the drag may have been
            // discarded as our own readout.
            ReArmIfLandedAtTheBottom();
        }

        void OnPump()
        {
            // Counted on every tick, before anything can return: this is the surface's clock, and a
            // consumer that has to wait for a layout has nothing else to wait on.
            PumpTick++;

            // An undisplayed subtree is zeroed out rather than laid out, so a pass over it records every
            // slot at a height of nothing and leaves the model describing a conversation of nothing. The
            // panel undisplays the surface whenever the conversation is empty.
            if (resolvedStyle.display == DisplayStyle.None)
            {
                m_Undisplayed = true;
                return;
            }

            if (m_Undisplayed)
            {
                // Coming back raises nothing of its own: the subtree returns to the layout it left.
                m_Undisplayed = false;
                RequestPass();
            }

            // Passes run on request, and unconditionally until the first paint: a pass is the only thing
            // that can measure what the reveal gate waits for, so the gate has to drive its own.
            if (!m_PassRequested && ContentRevealed)
                return;

            m_PassRequested = false;

            // Counted before the pass and spent after it: a pass that throws still has to bring the
            // deadline closer, and the reveal belongs after the pass that positioned what it shows.
            if (!ContentRevealed)
                m_FirstPaintPasses++;

            Invoke(PassStarting);

            if (m_Model.StickToBottom || ConsumeLandAtEnd())
                m_Model.MoveToEnd(ViewportHeight);

            Invoke(PassRequested);
            UpdateScroller();
            NotifyIfTheTotalHeightMoved();

            if (!ContentRevealed && m_FirstPaintPasses >= k_MaxFirstPaintFrames)
                Reveal();
        }

        /// <summary>
        /// Whether this pass owes the one-shot landing its move to the end. The landing spans the whole
        /// first paint deliberately: those are the passes that measure the tail, so only the last of them
        /// can find an end the reader is still at once the heights are real.
        /// </summary>
        bool ConsumeLandAtEnd()
        {
            if (!m_LandAtEndPending || ViewportHeight <= 0f)
                return false;

            m_LandAtEndPending = !ContentRevealed;
            return true;
        }

        /// <summary>
        /// Reports an estimated total that moved, which is the conversation growing under a reader who is
        /// not following it: the distance to the end grows with it, and the viewport whose geometry the
        /// event otherwise comes from has not changed at all.
        /// </summary>
        void NotifyIfTheTotalHeightMoved()
        {
            var total = m_Model.EstimatedTotal;

            if (Mathf.Approximately(total, m_LastTotalHeight))
                return;

            m_LastTotalHeight = total;
            Invoke(GeometryChanged);
        }

        // A pass runs the owner's re-flatten and the realizer's binds, so it runs foreign code: one
        // element that throws must cost its own item, not the pump, the rest of the conversation and
        // the reveal that follows.
        static void Invoke(Action pass)
        {
            try
            {
                pass?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Settles the wheel with the nested scroller under the pointer before the scroller itself can. Stock
        /// <see cref="ScrollView"/> reads a gesture against its own axes, and a view whose only usable axis is
        /// the horizontal one — a wide code block, table or tool call output — gets both directions wrong:
        /// it spends a vertical delta on that axis, panning sideways under a gesture aimed at the
        /// conversation, and it never reads <c>delta.x</c> at all, so a genuine sideways gesture moves it
        /// nowhere. Whatever stock does place on the axis the gesture was on is left alone, and reaches
        /// <see cref="OnWheel"/> on the way back up if the nested view had no use for it.
        /// </summary>
        void OnWheelBeforeDescendants(WheelEvent evt)
        {
            if (IsZoomGesture(evt))
                return;

            for (var element = evt.target as VisualElement; element != null && element != Viewport; element = element.hierarchy.parent)
            {
                if (element is not ScrollView view)
                    continue;

                var alwaysStops = view.nestedInteractionKind == ScrollView.NestedInteractionKind.StopScrolling;
                var scrollSize = view.mouseWheelScrollSize;
                var canPanSideways = view.mode != ScrollViewMode.Vertical && view.horizontalScroller.highValue > 0f;

                if (view.mode != ScrollViewMode.Horizontal && view.verticalScroller.highValue > 0f)
                {
                    // Stock spends a vertical delta on the vertical axis and a sideways one on the horizontal
                    // axis, so whichever of them it moves, it moves the axis the gesture was on.
                    if (alwaysStops
                        || WouldScroll(view.verticalScroller, evt.delta.y, scrollSize)
                        || (canPanSideways && WouldScroll(view.horizontalScroller, evt.delta.x, scrollSize)))
                        return;

                    continue;
                }

                if (!canPanSideways)
                    continue;

                var sideways = SidewaysDelta(evt);

                if (!Mathf.Approximately(sideways, 0f))
                {
                    PanSideways(view, sideways * scrollSize);
                    evt.StopPropagation();
                    return;
                }

                if (alwaysStops || WouldScroll(view.horizontalScroller, evt.delta.y, scrollSize))
                {
                    OnWheel(evt);
                    return;
                }
            }
        }

        void OnWheel(WheelEvent evt)
        {
            if (IsZoomGesture(evt))
                return;

            MoveByUserInput(evt.delta.y * k_LineHeight);
            evt.StopPropagation();
        }

        // Matches stock ScrollView: a zoom gesture is not a scroll.
        static bool IsZoomGesture(WheelEvent evt) => evt.commandKey || evt.ctrlKey;

        /// <summary>
        /// The sideways part of a gesture, for a view stock would read nothing sideways on. A trackpad swipe
        /// arrives as <c>delta.x</c>, which is the axis IMGUI's own scroll view pans on and the one stock
        /// spends sideways whenever it has a vertical axis beside it. A wheel held under shift arrives as
        /// <c>delta.x</c> where the platform converts it and as <c>delta.y</c> where it does not, and there
        /// the modifier is the only thing that tells it apart from a gesture meant for the conversation.
        /// </summary>
        static float SidewaysDelta(WheelEvent evt)
        {
            if (Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y))
                return evt.delta.x;

            return evt.shiftKey ? evt.delta.y : 0f;
        }

        /// <summary>Pans a nested view the way stock writes onto a scroller: along whichever way the range
        /// runs, and left to the slider to clamp at the end of the travel.</summary>
        static void PanSideways(ScrollView view, float pixels)
        {
            var scroller = view.horizontalScroller;
            scroller.value += pixels * (scroller.lowValue < scroller.highValue ? 1f : -1f);
        }

        /// <summary>
        /// Whether stock's write onto a scroller would land anywhere other than where it already is. It
        /// adds the delta along whichever way the range runs and the slider clamps the result, so a
        /// scroller already against the limit the gesture points at reports no change — which is stock's
        /// own evidence that the wheel was never its to keep.
        /// </summary>
        static bool WouldScroll(Scroller scroller, float delta, float scrollSize)
        {
            var travel = delta * (scroller.lowValue < scroller.highValue ? 1f : -1f) * scrollSize;

            if (Mathf.Approximately(travel, 0f))
                return false;

            var limit = travel > 0f
                ? Mathf.Max(scroller.lowValue, scroller.highValue)
                : Mathf.Min(scroller.lowValue, scroller.highValue);

            return !Mathf.Approximately(scroller.value, limit);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            // Pen and touch are the only drag path; a mouse press is a click on content, not a scroll.
            if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse)
                return;

            // Only the press is recorded here. A tap that never moves is not a scroll, and taking the
            // view off the tail on it would end the follow for a reader who only touched the screen.
            m_DragPointerId = evt.pointerId;
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_DragPointerId != evt.pointerId)
                return;

            MoveByUserInput(-evt.deltaPosition.y);
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (m_DragPointerId == evt.pointerId)
                m_DragPointerId = k_NoPointer;
        }

        void OnPointerLeave(PointerLeaveEvent evt) => m_DragPointerId = k_NoPointer;

        void OnScrollerValueChanged(float value)
        {
            // BaseSlider clamps its value when highValue shrinks and the clamp notifies, so our own
            // readout writes arrive here — immediately while we write, or later when a dispatcher gate
            // queues them. Only a value the slider was not already holding is a gesture, and only a
            // gesture may move the anchor.
            if (m_WritingScroller || Mathf.Approximately(value, m_LastScrollerValue))
                return;

            Follow(tail: false);

            if (IsScrollerHeld())
                ScrubBy(value);
            else
                SetAnchorFromOffset(value);

            m_LastScrollerValue = value;
            ReArmIfLandedAtTheBottom();
            RequestPass();
            UserScrolled?.Invoke();
        }

        /// <summary>
        /// Where a pixel offset from the scrollbar puts the anchor. Sound only while the range the offset
        /// came from describes the tree it is read against, which is true of a value that arrives with no
        /// pointer on the scroller — one write, against the tree as it stands.
        /// </summary>
        void SetAnchorFromOffset(float value)
        {
            var item = m_Heights.ItemAtOffset(value);
            m_Model.Set(item, value - m_Heights.OffsetOf(item), ViewportHeight);
        }

        /// <summary>
        /// Spends the distance a gesture on the scroller covered as content pixels, rather than mapping
        /// where the knob now sits onto an absolute offset. Every gesture the scroller reads — a knob
        /// drag, a track page, an arrow's repeat — is computed against a range frozen for the whole of
        /// it, while each window the drag lands on replaces estimates with measured heights and moves the
        /// tree underneath: the same knob position read against a total that has since changed means
        /// somewhere else on every tick. A distance cannot drift like that. The live tree only changes
        /// the rate of the steps that follow it, so equal knob distances cover equal shares of the
        /// conversation, which is what makes the scrollbar track the content the way the wheel does.
        /// </summary>
        void ScrubBy(float value)
        {
            // Held against a stop, the reader is asking for that end of the conversation whatever the
            // scrub adds up to. The slider clamps its own value there, so nothing accumulates past it.
            if (value <= VerticalScroller.lowValue)
            {
                m_Model.MoveToStart();
                return;
            }

            if (value >= VerticalScroller.highValue)
            {
                m_Model.MoveToEnd(ViewportHeight);
                return;
            }

            var scrollable = Mathf.Max(0f, m_Model.EstimatedTotal - ViewportHeight);
            m_Model.MoveBy((value - m_LastScrollerValue) * scrollable / VerticalScroller.highValue, ViewportHeight);
        }

        void OnViewportGeometryChanged(GeometryChangedEvent evt)
        {
            // The layout zeroes an undisplayed subtree out and reports that as a geometry change. It is
            // not a resize: taking that width drops every measurement the conversation holds, and taking
            // that height moves the anchor to fit a viewport nobody is looking at.
            if (resolvedStyle.display == DisplayStyle.None)
                return;

            // Ahead of the width invalidation: the anchor is corrected against the heights the view was
            // showing, and a dropped measurement would clamp it onto an estimate instead.
            m_Model.SetViewportHeight(ViewportHeight);
            m_Heights.InvalidateForWidth(Content.resolvedStyle.width);
            RequestPass();
            GeometryChanged?.Invoke();
        }

        void MoveByUserInput(float delta)
        {
            ScrollBy(delta);
            UserScrolled?.Invoke();
        }

        /// <summary>Points the surface at the tail, or off it. Either way a pending one-shot landing is
        /// spent: a caller that named a position of its own supersedes it.</summary>
        void Follow(bool tail)
        {
            m_Model.StickToBottom = tail;
            m_LandAtEndPending = false;
        }

        /// <summary>
        /// Re-engages the tail follow when a user gesture came to rest at the bottom, which is what the
        /// legacy list re-locked on. Without it one wheel tick during a streamed answer ends the follow
        /// for good, and scrolling back onto the newest message never brings it back.
        /// </summary>
        void ReArmIfLandedAtTheBottom()
        {
            if (m_Model.IsAtBottom(ViewportHeight))
                m_Model.StickToBottom = true;
        }

        void Reveal()
        {
            ContentRevealed = true;
            Content.SetVisible(true);

            // The pass that spends the landing is the one after the reveal, and nothing else asks for it.
            if (m_LandAtEndPending)
                RequestPass();

            // The conversation becoming visible: this is the number "first load is almost instant" is judged on.
            AssistantPerf.MarkElapsed("chat.first_paint", "passes=" + m_FirstPaintPasses + ";visible=" + m_FirstPaintItems, m_FirstPaintTicks);

            FirstPaintCompleted?.Invoke();
        }
    }
}
