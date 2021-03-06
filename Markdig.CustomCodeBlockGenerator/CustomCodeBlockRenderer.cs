﻿using System.Collections.Generic;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Markdig.CustomCodeBlockGenerator
{
    /// <inheritdoc />
    /// <summary>
    /// Custom code block renderer.
    /// Instead of default html markup for code blocks adds also custom tags with pattern
    /// </summary>
    public class CustomCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private readonly CodeBlockRenderer _underlyingRenderer;
        public CustomCodeBlockRenderer(CodeBlockRenderer underlyingRenderer = null)
        {
            _underlyingRenderer = underlyingRenderer ?? new CodeBlockRenderer();
        }

        /* pattern:
        <div class="veracity-dev-pres-html-code" data-lang="language-javascript">
        <div class="veracity-dev-pres-html-code-header">
        <strong>JavaScript</strong>
        <button>Copy</button>
        </div>
        <pre>
        <code class="language-javascript">
            ...
        </code>
        </pre>
        </div> */
        protected override void Write(HtmlRenderer renderer, CodeBlock obj)
        {
            if (!(obj is FencedCodeBlock fencedCodeBlock) || !(obj.Parser is FencedCodeBlockParser parser))
            {
                _underlyingRenderer.Write(renderer, obj);
                return;
            }
            var languageMoniker = fencedCodeBlock.Info.Replace(parser.InfoPrefix, string.Empty);
            if (string.IsNullOrEmpty(languageMoniker))
            {
                _underlyingRenderer.Write(renderer, obj);
                return;
            }

            renderer
                .Write("<div")
                .WriteAttributes(new HtmlAttributes
                {
                    Classes = new List<string> {"veracity-dev-pres-html-code"},
                    Properties =
                        new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("data-lang", $"language-{languageMoniker}")
                        }
                })
                .WriteLine(">")
                .Write("<div")
                .WriteAttributes(new HtmlAttributes
                {
                    Classes = new List<string> {"veracity-dev-pres-html-code-header"}
                })
                .WriteLine(">")
                .WriteLine("<strong></strong>")
                .WriteLine("<button>Copy</button>")
                .WriteLine("</div>")
                .Write("<pre>")
                .Write("<code")
                .WriteAttributes(obj)
                .Write(">")
                .WriteLeafRawLines(obj, false, true)
                .Write("</code>")
                .WriteLine("</pre>")
                .WriteLine("</div>");
        }
    }
}