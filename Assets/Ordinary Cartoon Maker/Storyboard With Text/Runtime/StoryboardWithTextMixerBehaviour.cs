using UnityEngine.Playables;

namespace UnityEngine.Sequences.Timeline
{
    public class StoryboardWithTextMixerBehaviour : PlayableBehaviour
    {
        Canvas m_Canvas;

        public Canvas canvas => m_Canvas;

        /// <inheritdoc cref="PlayableBehaviour.OnPlayableCreate"/>
        public override void OnPlayableCreate(Playable playable)
        {
            var canvasGo = new GameObject("Storyboard");
            canvasGo.hideFlags = HideFlags.HideAndDontSave;
            m_Canvas = canvasGo.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <inheritdoc cref="PlayableBehaviour.OnPlayableDestroy"/>
        public override void OnPlayableDestroy(Playable playable)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(m_Canvas.gameObject);
#else
            Object.Destroy(m_Canvas.gameObject);
#endif
        }
    }
}
