using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;
using System.Threading;

namespace MyDigitalLibrary.Core.Services;

public interface IGoogleBooksService
{
    /// <summary>
    /// Search Google Books by title and/or author. Either parameter may be null or empty.
    /// </summary>
    Task<GoogleBook[]> SearchByTitleAsync(string? title, string? author = null, int maxResults = 10, CancellationToken cancellation = default);

    /// <summary>
    /// Apply selected fields from a GoogleBook to an existing BookEntity. If overwrite is false, only empty fields are populated.
    /// </summary>
    void ApplyToEntity(BookEntity entity, GoogleBook source, bool overwrite = false);
}
