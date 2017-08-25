## Overview
This repo contains the code for Markdown Parser project used to parse .md files to HTML code together with additional metadata like headers tree structure for automatic table of contents generation.
MarkdownParserFunction is Azure Fuction, which waits for event from GitHub (Webhook) with incoming commit, filters files in commit against .md extension.
If any files found, MarkdownParser produces JSON fo reach, containing parsed HTML and metadata.
JSON files are at the end put into blob for further operations.

### Useful articles:
JSON schema from GitHub Webhook:

https://developer.github.com/v3/activity/events/types/#pushevent 

Azure Functions Imperative bindings reference:

https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings
