using System;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Markdig.CustomCodeBlockGenerator
{
    /// <inheritdoc />
    /// <summary>
    /// Custom extension to replace default CodeBlockRenderer with our own.
    /// Original renderer is removed and custom one added on its place.
    /// </summary>
    public class CustomCodeBlockExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) { }
        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            if (!(renderer is TextRendererBase<HtmlRenderer> htmlRenderer))
                return;

            var originalCodeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (originalCodeBlockRenderer != null)
                htmlRenderer.ObjectRenderers.Remove(originalCodeBlockRenderer);

            htmlRenderer.ObjectRenderers.AddIfNotAlready(
                new CustomCodeBlockRenderer(originalCodeBlockRenderer));
        }
    }
}