using System.Text;

namespace SmartCollectAPI.Services;

public interface ITextChunkingService
{
    List<TextChunk> ChunkText(string text, ChunkingOptions? options = null);
}

public record TextChunk(
    string Content,
    int StartOffset,
    int EndOffset,
    int ChunkIndex,
    Dictionary<string, object> Metadata
);

public record ChunkingOptions(
    int MaxTokens = 512,
    int OverlapTokens = 100,
    ChunkingStrategy Strategy = ChunkingStrategy.SlidingWindow
);

public enum ChunkingStrategy
{
    SlidingWindow,
    Sentence,
    Paragraph
}

public class TextChunkingService(ILogger<TextChunkingService> logger) : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger = logger;

    public List<TextChunk> ChunkText(string text, ChunkingOptions? options = null)
    {
        options ??= new ChunkingOptions();

        return options.Strategy switch
        {
            ChunkingStrategy.SlidingWindow => ChunkBySlidingWindow(text, options),
            ChunkingStrategy.Sentence => ChunkBySentence(text, options),
            ChunkingStrategy.Paragraph => ChunkByParagraph(text, options),
            _ => ChunkBySlidingWindow(text, options)
        };
    }

    private List<TextChunk> ChunkBySlidingWindow(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();

        // Rough token approximation: 1 token ≈ 4 characters
        var charsPerChunk = options.MaxTokens * 4;
        var overlapChars = options.OverlapTokens * 4;

        var currentPosition = 0;
        var chunkIndex = 0;

        while (currentPosition < text.Length)
        {
            var remainingLength = text.Length - currentPosition;
            var chunkLength = Math.Min(charsPerChunk, remainingLength);

            // Try to break at sentence boundary if possible
            if (currentPosition + chunkLength < text.Length)
            {
                var endPosition = currentPosition + chunkLength;
                var lastPeriod = text.LastIndexOf(". ", endPosition, Math.Min(100, chunkLength));

                if (lastPeriod > currentPosition)
                {
                    chunkLength = lastPeriod - currentPosition + 2; // Include the period and space
                }
            }

            var content = text.Substring(currentPosition, chunkLength);

            chunks.Add(new TextChunk(
                Content: content,
                StartOffset: currentPosition,
                EndOffset: currentPosition + chunkLength,
                ChunkIndex: chunkIndex++,
                Metadata: new Dictionary<string, object>
                {
                    ["strategy"] = "sliding_window",
                    ["char_count"] = chunkLength,
                    ["approx_tokens"] = chunkLength / 4
                }
            ));

            // Move position forward, accounting for overlap
            currentPosition += chunkLength - overlapChars;

            // Avoid infinite loop
            if (currentPosition >= text.Length - overlapChars)
                break;
        }

        _logger.LogInformation("Chunked {TextLength} characters into {ChunkCount} chunks",
            text.Length, chunks.Count);

        return chunks;
    }

    private List<TextChunk> ChunkBySentence(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        var sentences = SplitIntoSentences(text);

        var currentChunk = new StringBuilder();
        var currentStartOffset = 0;
        var currentChunkIndex = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;
            var currentLength = currentChunk.Length;

            // Check if adding this sentence would exceed max tokens
            if (currentLength > 0 && (currentLength + sentenceLength) / 4 > options.MaxTokens)
            {
                // Flush current chunk
                chunks.Add(new TextChunk(
                    Content: currentChunk.ToString(),
                    StartOffset: currentStartOffset,
                    EndOffset: currentStartOffset + currentLength,
                    ChunkIndex: currentChunkIndex++,
                    Metadata: new Dictionary<string, object>
                    {
                        ["strategy"] = "sentence",
                        ["sentence_count"] = currentChunk.ToString().Split(". ").Length
                    }
                ));

                currentChunk.Clear();
                currentStartOffset += currentLength;
            }

            currentChunk.Append(sentence);
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk(
                Content: currentChunk.ToString(),
                StartOffset: currentStartOffset,
                EndOffset: currentStartOffset + currentChunk.Length,
                ChunkIndex: currentChunkIndex,
                Metadata: new Dictionary<string, object>
                {
                    ["strategy"] = "sentence",
                    ["sentence_count"] = currentChunk.ToString().Split(". ").Length
                }
            ));
        }

        return chunks;
    }

    private static List<TextChunk> ChunkByParagraph(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var currentStartOffset = 0;
        var currentChunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphLength = paragraph.Length;
            var currentLength = currentChunk.Length;

            // Check if adding this paragraph would exceed max tokens
            if (currentLength > 0 && (currentLength + paragraphLength) / 4 > options.MaxTokens)
            {
                // Flush current chunk
                chunks.Add(new TextChunk(
                    Content: currentChunk.ToString().Trim(),
                    StartOffset: currentStartOffset,
                    EndOffset: currentStartOffset + currentLength,
                    ChunkIndex: currentChunkIndex++,
                    Metadata: new Dictionary<string, object>
                    {
                        ["strategy"] = "paragraph"
                    }
                ));

                currentChunk.Clear();
                currentStartOffset += currentLength;
            }

            if (currentChunk.Length > 0)
                currentChunk.Append("\n\n");

            currentChunk.Append(paragraph);
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk(
                Content: currentChunk.ToString().Trim(),
                StartOffset: currentStartOffset,
                EndOffset: text.Length,
                ChunkIndex: currentChunkIndex,
                Metadata: new Dictionary<string, object>
                {
                    ["strategy"] = "paragraph"
                }
            ));
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting - in production, use spaCy for better results
        var sentences = new List<string>();
        var sentenceEndings = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };

        var currentSentence = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            currentSentence.Append(text[i]);

            // Check if we're at a sentence boundary
            var remaining = text[i..];
            if (sentenceEndings.Any(ending => remaining.StartsWith(ending)))
            {
                sentences.Add(currentSentence.ToString());
                currentSentence.Clear();
            }
        }

        // Add any remaining text
        if (currentSentence.Length > 0)
        {
            sentences.Add(currentSentence.ToString());
        }

        return sentences;
    }
}
