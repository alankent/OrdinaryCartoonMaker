using System;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace UnityEngine.Sequences.Timeline
{
    /// <summary>
    /// Contains the logic for displaying and scaling the image to the screen size.
    /// </summary>
    public class StoryboardWithTextPlayableBehaviour : PlayableBehaviour
    {
        Texture m_Board = null;
        public Texture board
        {
            set => m_Board = value;
        }

        bool m_ShowBoard = true;
        public bool showBoard
        {
            set => m_ShowBoard = value;
        }

        Vector2 m_Position = Vector2.zero;
        public Vector2 position
        {
            set => m_Position = value;
        }

        float m_Alpha = 1;
        public float alpha
        {
            set => m_Alpha = value;
        }

        string m_Text = null;
        public string text
        {
            set => m_Text = value;
        }

        Color m_TextColor = Color.black;
        public Color textColor
        {
            set => m_TextColor = value;
        }

        Font m_Font;
        public Font font
        {
            set => m_Font = value;
        }

        int m_FontSize;
        public int fontSize
        {
            set => m_FontSize = value;
        }

        float m_LineSpacing;
        public float lineSpacing
        {
            set => m_LineSpacing = value;
        }

        Vector3 m_Rotation = Vector3.zero;
        public Vector3 rotation
        {
            set => m_Rotation = value;
        }

        Vector2 m_Scale = Vector2.one;
        public Vector2 scale
        {
            set => m_Scale = value;
        }

        float m_FadeIn = 0;
        public float fadeIn
        {
            set => m_FadeIn = value;
        }

        float m_FadeOut = 0;
        public float fadeOut
        {
            set => m_FadeOut = value;
        }

        Canvas m_Canvas;
        private RawImage m_currentBoard;
        private Text m_text;

        /// <inheritdoc cref="PlayableBehaviour.ProcessFrame"/>
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            ScriptPlayable<StoryboardWithTextMixerBehaviour> outputPlayable =
                (ScriptPlayable<StoryboardWithTextMixerBehaviour>)playable.GetOutput(0);

            var outputPlayableBehaviour = outputPlayable.GetBehaviour();
            m_Canvas = outputPlayableBehaviour.canvas;

            // Fade in/out the alpha. Time in seconds since start of clip and the duration of the clip.
            float alpha = m_Alpha;
            float time = (float)playable.GetTime();
            float clipDuration = (float)playable.GetDuration();
            float fadeInDuration = m_FadeIn * clipDuration;
            float fadeOutDuration = m_FadeOut * clipDuration;
            if (fadeInDuration > 0f && time < fadeInDuration)
            {
                alpha *= time / fadeInDuration;
            }
            if (fadeOutDuration >0f && time > clipDuration - fadeOutDuration)
            {
                alpha *= (clipDuration - time) / fadeOutDuration;
            }

            if (m_Canvas == null || (m_Board == null && m_Text == null) || !m_ShowBoard) return;

            if (m_Canvas.transform.childCount == 0)
            {
                if (m_Board != null)
                {
                    // if the board isn't loaded, load it
                    var boardGo = new GameObject(m_Board.name);
                    boardGo.hideFlags = HideFlags.HideAndDontSave;

                    boardGo.transform.parent = m_Canvas.transform;
                    boardGo.transform.localPosition = m_Position;
                    boardGo.transform.localScale = GetBestFitScale(m_Scale);

                    m_currentBoard = boardGo.AddComponent<RawImage>();
                    m_currentBoard.texture = m_Board;
                    // Using SetNativeSize because otherwise textures are resized to 100x100 by default
                    m_currentBoard.SetNativeSize();

                    var color = Color.white;
                    color.a = alpha;
                    m_currentBoard.color = color;

                    m_currentBoard.transform.rotation = Quaternion.Euler(m_Rotation);
                }

                if (m_Text != null)
                {
                    // Create the Text GameObject.
                    GameObject textGO = new GameObject();
                    textGO.hideFlags = HideFlags.HideAndDontSave;
                    textGO.transform.parent = m_Canvas.transform;
                    m_text = textGO.AddComponent<Text>();

                    // Set Text component properties.
                    // (If no background image, the alpha controls the text - else alpha controls the background image only)
                    m_text.text = m_Text;
                    m_text.color = m_TextColor;
                    if (m_Font != null) m_text.font = m_Font;
                    m_text.fontSize = m_FontSize;
                    m_text.lineSpacing = m_LineSpacing;
                    m_text.alignment = TextAnchor.MiddleCenter;
                    m_text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    m_text.verticalOverflow = VerticalWrapMode.Overflow;

                    m_text.transform.localPosition = m_Position;
                    m_text.transform.localScale = GetBestFitScale(Vector2.one);
                    m_text.transform.rotation = Quaternion.Euler(m_Rotation);
                }
            }
            else
            {
                // there is a board or text, rescale it & adjust rotation
                for (int i = 0; i < m_Canvas.transform.childCount; i++)
                {
                    var child = m_Canvas.transform.GetChild(i);
                    child.localScale = GetBestFitScale(child.GetComponent<Text>() == null ? m_Scale : Vector2.one);
                    child.rotation = Quaternion.Euler(m_Rotation);
                }
            }

            // Fade in/out (per frame) the board (if present) or text.
            if (m_currentBoard != null)
            {
                var color = m_currentBoard.color;
                color.a = alpha;
                m_currentBoard.color = color;
            }
            if (m_text != null)
            {
                var color = m_text.color;
                m_text.color = color;
            }
        }

        /// <summary>
        /// Calculates how much a board needs to be scaled in order to fit to the size of the current GameView
        /// Uses BestFit (does not affect original aspect ratio of the image)
        /// </summary>
        /// <returns>Vector2 that indicates how much to scale the orginial image to fit the GameView</returns>
        Vector2 GetBestFitScale(Vector2 scaleFactor)
        {
            // TODO: investigate how to get it to scale with Gameview size and aspect ratio changes
            Rect screen = new Rect((float)Screen.width / 2, (float)Screen.height / 2, Screen.width, Screen.height);

            Vector2 reScale = Vector2.one;
            if (m_Board != null
                && m_Board.width > 0 && m_Board.height > 0
                && screen.width > 0 && screen.height > 0)
            {
                // Fits the image to the size of the GameView without altering the aspect ratio
                float bestFitScale = Math.Min(screen.height / m_Board.height, screen.width / m_Board.width);
                reScale *= bestFitScale;
            }

            reScale.x *= scaleFactor.x;
            reScale.y *= scaleFactor.y;
            return reScale;
        }

        /// <inheritdoc cref="PlayableBehaviour.OnBehaviourPause"/>
        /// <remarks> When the playhead leaves the clip, remove the board from the canvas</remarks>
        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (m_Canvas == null) return;
            if (m_Canvas.transform.childCount <= 0) return;

            while (m_Canvas.transform.childCount > 0)
            {
                var child = m_Canvas.transform.GetChild(0);
                child.SetParent(null);
#if UNITY_EDITOR
                Object.DestroyImmediate(child.gameObject);
#else
                Object.Destroy(child.gameObject);
#endif
            }
        }
    }
}
