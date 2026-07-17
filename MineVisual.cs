using System.Collections.Generic;
using UnityEngine;

namespace MineSupport
{
    // Replaceable visual backend for Mine notes. The first implementation only tints sprites.
    internal static class MineVisual
    {
        private static readonly Dictionary<SpriteRenderer, Color> OriginalSpriteColors =
            new Dictionary<SpriteRenderer, Color>();

        private static readonly Color MineTint = new Color(0.68f, 0.68f, 0.68f, 1f);

        internal static void Apply(MonoBehaviour note, bool mine)
        {
            if (note == null)
                return;

            var renderers = note.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!mine)
                {
                    Color original;
                    if (OriginalSpriteColors.TryGetValue(renderer, out original))
                    {
                        renderer.color = original;
                        OriginalSpriteColors.Remove(renderer);
                    }

                    continue;
                }

                if (!OriginalSpriteColors.ContainsKey(renderer))
                    OriginalSpriteColors.Add(renderer, renderer.color);

                var color = renderer.color;
                color.r = MineTint.r;
                color.g = MineTint.g;
                color.b = MineTint.b;
                renderer.color = color;
            }
        }

        internal static void Clear(MonoBehaviour note)
        {
            Apply(note, false);
        }
    }
}
