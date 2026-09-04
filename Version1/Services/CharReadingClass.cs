using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace SQCScanner.Services
{
    public class CharReadingClass
    {
        // Rotation angles to try, in the order we try them.
        // 0 first because most boxes are already upright — saves time in the common case.
        private static readonly float[] RotationAngles = { 0f, 90f, 180f, 270f };

        public static string ReadChar(Image<Rgba32> charBox)
        {
            try
            {
                string bestResult = string.Empty;
                float bestConfidence = -1f;

                bool needsUpscale = charBox.Height < 40 || charBox.Width < 40;

                // Reuse a single engine instance across rotation attempts instead of
                // creating a new TesseractEngine per attempt (engine init is expensive).
                using var engine = new TesseractEngine("./tessdata", "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "0123456789");

                foreach (var angle in RotationAngles)
                {
                    // Clone so we don't mutate the original box, and so each
                    // rotation attempt starts from the same source image.
                    using var candidate = charBox.Clone(ctx =>
                    {
                        // Upscale small crops — Tesseract accuracy drops a lot below ~40px tall.
                        if (needsUpscale)
                        {
                            const int scale = 3;
                            ctx.Resize(charBox.Width * scale, charBox.Height * scale, KnownResamplers.Lanczos3);
                        }

                        if (angle != 0f)
                        {
                            ctx.Rotate(angle);
                        }

                        // Light contrast boost helps thin/faint printed digits.
                        ctx.Grayscale();
                        ctx.Contrast(1.2f);
                    });

                    using var stream = new MemoryStream();
                    candidate.SaveAsPng(stream);
                    stream.Position = 0;

                    using var pix = Pix.LoadFromMemory(stream.ToArray());
                    using var page = engine.Process(pix, PageSegMode.SingleLine);

                    string rawText = page.GetText();
                    string digitsOnly = new string(rawText.Where(char.IsDigit).ToArray());
                    float confidence = page.GetMeanConfidence();

                    if (!string.IsNullOrEmpty(digitsOnly) && confidence > bestConfidence)
                    {
                        bestConfidence = confidence;
                        bestResult = digitsOnly;
                    }
                }

                return bestResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}