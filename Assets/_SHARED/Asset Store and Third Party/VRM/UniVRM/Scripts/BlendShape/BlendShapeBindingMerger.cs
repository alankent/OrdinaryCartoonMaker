using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRM
{
    ///
    /// A.Value * A.Weight + B.Value * B.Weight ...
    ///
    class BlendShapeBindingMerger
    {
        class DictionaryKeyBlendShapeBindingComparer : IEqualityComparer<BlendShapeBinding>
        {
            public bool Equals(BlendShapeBinding x, BlendShapeBinding y)
            {
                return x.RelativePath == y.RelativePath
                && x.Index == y.Index;
            }

            public int GetHashCode(BlendShapeBinding obj)
            {
                return obj.RelativePath.GetHashCode() + obj.Index;
            }
        }

        private static DictionaryKeyBlendShapeBindingComparer comparer = new DictionaryKeyBlendShapeBindingComparer();

        /// <summary>
        /// BlendShapeの適用値を蓄積する
        /// </summary>
        /// <typeparam name="BlendShapeBinding"></typeparam>
        /// <typeparam name="float"></typeparam>
        /// <returns></returns>
        Dictionary<BlendShapeBinding, float> m_blendShapeValueMap = new Dictionary<BlendShapeBinding, float>(comparer);

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        Dictionary<BlendShapeBinding, Action<float>> m_blendShapeSetterMap = new Dictionary<BlendShapeBinding, Action<float>>(comparer);

        public BlendShapeBindingMerger(Dictionary<BlendShapeKey, BlendShapeClip> clipMap, Transform root)
        {
            foreach (var kv in clipMap)
            {
                foreach (var binding in kv.Value.Values)
                {
                    if (!m_blendShapeSetterMap.ContainsKey(binding))
                    {
                        var _target = root.Find(binding.RelativePath);
                        SkinnedMeshRenderer target = null;
                        if (_target != null)
                        {
                            target = _target.GetComponent<SkinnedMeshRenderer>();
                        }
                        if (target != null)
                        {
                            if (binding.Index >= 0 && binding.Index < target.sharedMesh.blendShapeCount)
                            {
                                m_blendShapeSetterMap.Add(binding, x =>
                                {
                                    target.SetBlendShapeWeight(binding.Index, x);
                                });
                            }
                            else
                            {
                                Debug.LogWarningFormat("Invalid blendshape binding: {0}: {1}", target.name, binding);
                            }

                        }
                        else
                        {
                            Debug.LogWarningFormat("SkinnedMeshRenderer: {0} not found ({1})", binding.RelativePath, binding.ToString());
                        }
                    }
                }
            }
        }

        public void ImmediatelySetValue(BlendShapeClip clip, float value)
        {
            foreach (var binding in clip.Values)
            {
                Action<float> setter;
                if (m_blendShapeSetterMap.TryGetValue(binding, out setter))
                {
                    setter(binding.Weight * value);
                }
            }
        }

        public void AccumulateValue(BlendShapeClip clip, float value)
        {
            foreach (var binding in clip.Values)
            {
                //if (binding.Index == 27 /* O */) Debug.Log("AV:27:v = " + value * binding.Weight);
                float acc;
                if (m_blendShapeValueMap.TryGetValue(binding, out acc))
                {
                    m_blendShapeValueMap[binding] = acc + binding.Weight * value;
                }
                else
                {
                    m_blendShapeValueMap[binding] = binding.Weight * value;
                }
            }
        }

        public void Apply()
        {
            foreach (var kv in m_blendShapeValueMap)
            {
                //if (kv.Key.Index == 27) Debug.Log("APPLY:27");
                Action<float> setter;
                if (m_blendShapeSetterMap.TryGetValue(kv.Key, out setter))
                {
                    //if (kv.Key.Index == 27) Debug.Log("APPLY:27:SETTER v=" + kv.Value);
                    setter(kv.Value);
                }
            }
            m_blendShapeValueMap.Clear();
        }
    }
}
