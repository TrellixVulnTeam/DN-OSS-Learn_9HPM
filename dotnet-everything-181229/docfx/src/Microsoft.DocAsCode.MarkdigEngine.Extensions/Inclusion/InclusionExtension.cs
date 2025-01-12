// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.MarkdigEngine.Extensions
{
    using System.Threading;
    using Markdig;
    using Markdig.Parsers;
    using Markdig.Parsers.Inlines;
    using Markdig.Renderers;
    using Markdig.Syntax;
    using Markdig.Syntax.Inlines;

    /// <summary>
    /// Extension to enable extension IncludeFile.
    /// </summary>
    public class InclusionExtension : IMarkdownExtension
    {
        private readonly MarkdownContext _context;
        private MarkdownPipeline _inlinePipeline;

        public InclusionExtension(MarkdownContext context)
        {
            _context = context;
        }

        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            pipeline.BlockParsers.AddIfNotAlready<InclusionBlockParser>();
            pipeline.InlineParsers.InsertBefore<LinkInlineParser>(new InclusionInlineParser());

            pipeline.DocumentProcessed += InclusionExtension.GetProcessDocumentDelegate(_context);
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                if (!htmlRenderer.ObjectRenderers.Contains<HtmlInclusionInlineRenderer>())
                {
                    var inlinePipeline = LazyInitializer.EnsureInitialized(ref _inlinePipeline, () => CreateInlineOnlyPipeline(pipeline));

                    htmlRenderer.ObjectRenderers.Insert(0, new HtmlInclusionInlineRenderer(_context, inlinePipeline));
                }

                if (!htmlRenderer.ObjectRenderers.Contains<HtmlInclusionBlockRenderer>())
                {
                    htmlRenderer.ObjectRenderers.Insert(0, new HtmlInclusionBlockRenderer(_context, pipeline));
                }
            }
        }

        public static ProcessDocumentDelegate GetProcessDocumentDelegate(MarkdownContext context)
        {
            return document => UpdateLinks(document, context);
        }

        private static void UpdateLinks(MarkdownObject markdownObject, MarkdownContext context)
        {
            if (markdownObject == null || context == null) return;

            if (markdownObject is ContainerBlock containerBlock)
            {
                foreach (var subBlock in containerBlock)
                {
                    UpdateLinks(subBlock, context);
                }
            }
            else if (markdownObject is LeafBlock leafBlock)
            {
                if (leafBlock.Inline != null)
                {
                    foreach (var subInline in leafBlock.Inline)
                    {
                        UpdateLinks(subInline, context);
                    }
                }
            }
            else if (markdownObject is ContainerInline containerInline)
            {
                foreach (var subInline in containerInline)
                {
                    UpdateLinks(subInline, context);
                }

                if (markdownObject is LinkInline linkInline && !linkInline.IsAutoLink)
                {
                    linkInline.GetDynamicUrl = () => context.GetLink(linkInline.Url, InclusionContext.File, InclusionContext.RootFile);
                }
            }
        }

        private static MarkdownPipeline CreateInlineOnlyPipeline(MarkdownPipeline pipeline)
        {
            var builder = new MarkdownPipelineBuilder();

            foreach (var extension in pipeline.Extensions)
            {
                builder.Extensions.Add(extension);
            }

            builder.UseInlineOnly();

            return builder.Build();
        }
    }
}
