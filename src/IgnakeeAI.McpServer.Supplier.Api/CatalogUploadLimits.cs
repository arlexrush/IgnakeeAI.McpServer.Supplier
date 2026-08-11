using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace IgnakeeAI.McpServer.Supplier.Api
{
    internal static class CatalogUploadLimits
    {
        public const long MaxFileBytes = 10 * 1024 * 1024;
        public const long MaxRequestBytes = MaxFileBytes + (64 * 1024);
        private const int MaxXlsxEntries = 1_000;
        private const long MaxXlsxUncompressedBytes = 100 * 1024 * 1024;

        public static bool TryValidate(
            IFormFile file,
            string requiredExtension,
            string requiredContentType,
            out string error)
        {
            if (file.Length == 0)
            {
                error = "El archivo no puede estar vacío.";
                return false;
            }

            if (file.Length > MaxFileBytes)
            {
                error = "El archivo supera el tamaño máximo permitido de 10 MB.";
                return false;
            }

            if (!string.Equals(
                    Path.GetExtension(file.FileName),
                    requiredExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"Solo se admiten archivos {requiredExtension}.";
                return false;
            }

            if (!string.Equals(
                    file.ContentType,
                    requiredContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"El tipo MIME debe ser {requiredContentType}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool IsSafeXlsx(IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                if (archive.Entries.Count > MaxXlsxEntries ||
                    archive.GetEntry("[Content_Types].xml") is null ||
                    archive.GetEntry("xl/workbook.xml") is null)
                {
                    return false;
                }

                long uncompressedBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    checked
                    {
                        uncompressedBytes += entry.Length;
                    }

                    if (uncompressedBytes > MaxXlsxUncompressedBytes)
                        return false;
                }

                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
