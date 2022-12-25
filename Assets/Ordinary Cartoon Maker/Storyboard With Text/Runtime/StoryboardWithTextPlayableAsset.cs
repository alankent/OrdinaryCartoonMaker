using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEngine.Sequences.Timeline
{
    /// <summary>
    ///  Contains clip data to be passed to StoryboardPlayableBehaviour
    /// </summary>
    public class StoryboardWithTextPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("If checked, the specified board will be displayed as an overlay over the virtual camera's output")]
        [SerializeField]
        public bool showBoard = true;

        [Tooltip("The board to be displayed")]
        [SerializeField]
        public Texture board;

        [Tooltip("The opacity of the board.  0 is transparent, 1 is opaque")]
        [Range(0, 1)]
        [SerializeField]
        public float alpha = 0.8f;

        [Tooltip("Text to be displayed")]
        [TextArea(5, 10)]
        [SerializeField]
        public string text;

        [Tooltip("Color of text")]
        [SerializeField]
        public Color textColor = Color.black;

        [Tooltip("Font")]
        [SerializeField]
        public Font font;

        [Tooltip("Font size")]
        [SerializeField]
        public int fontSize = 36;

        [Tooltip("Line spacing")]
        [SerializeField]
        public float lineSpacing = 2.2f;

        [Tooltip("The screen-space position at which to display the board. Zero is center")]
        [SerializeField]
        public Vector2 position = Vector2.zero;

        [Tooltip("The z-axis rotation to apply to the board")]
        [SerializeField]
        [Range(-180, 180)]
        public float zRotation = 0;

        // TODO: Move functionality to UI
        [Tooltip("If checked, the board will be flipped horizontally")]
        public bool horizontalFlip = false;

        [Tooltip("If checked, the board will be flipped vertically")]
        public bool verticalFlip = false;

        [Tooltip("If checked, X and Y scale are synchronized")]
        public bool syncScale = true;

        // TODO: Move scale range limit to UI
        [Tooltip("The screen-space scaling to apply to the board")]
        [Min(0)]
        public Vector2 scale = Vector2.one;

        [Tooltip("Fraction of clip to fade alpha in (0.5 = half)")]
        [Range(0f, 1f)]
        public float fadeIn = 0f;

        [Tooltip("Fraction of clip to fade alpha out (0.5 = half)")]
        [Range(0f, 1f)]
        public float fadeOut = 0f;

        // TODO: Add blending
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<StoryboardWithTextPlayableBehaviour>.Create(graph);

            if (playable.GetBehaviour() != null)
                SetAttributes(playable.GetBehaviour());

            return playable;
        }

        public void OnEnable()
        {
            // Cannot initialize fonts in the initializer section.
            //TODO: Comes up with type conflict in UI: if (font == null) font = new Font("CCComicrazy-Roman");
        }

        void SetAttributes(StoryboardWithTextPlayableBehaviour storyboardBehaviour)
        {
            storyboardBehaviour.showBoard = showBoard;
            storyboardBehaviour.board = board;
            storyboardBehaviour.position = position;
            storyboardBehaviour.alpha = alpha;
            storyboardBehaviour.text = text;
            storyboardBehaviour.textColor = textColor;
            storyboardBehaviour.font = (font == null) ? new Font("CCComicrazy-Roman") : font; // TODO: DOES NOT WORK.
            storyboardBehaviour.fontSize = fontSize;
            storyboardBehaviour.lineSpacing = lineSpacing;
            storyboardBehaviour.rotation = new Vector3(0, 0, zRotation);
            storyboardBehaviour.fadeIn = fadeIn;
            storyboardBehaviour.fadeOut = fadeOut;

            // To move to custom UI
            if (syncScale)
                scale.y = scale.x;

            var newScale = scale;

            if (horizontalFlip) newScale.x *= -1;
            if (verticalFlip) newScale.y *= -1;

            storyboardBehaviour.scale = newScale;
        }
    }
}
