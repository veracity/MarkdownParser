namespace Markdig.CustomCodeBlockGenerator
{
    public static class CustomCodeBlockExtensions
    {
        /// <summary>
        /// Method to be used on MarkdownPipelineBuilder when this extension should be used.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <returns></returns>
        public static MarkdownPipelineBuilder UseCustomCodeBlockExtension(this MarkdownPipelineBuilder pipeline)
        {
            pipeline.Extensions.Add(new CustomCodeBlockExtension());
            return pipeline;
        }
    }
}
