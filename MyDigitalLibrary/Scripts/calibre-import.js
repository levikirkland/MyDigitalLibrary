const Database = require('better-sqlite3')
const fs = require('fs')
const path = require('path')
const { parseStringPromise } = require('xml2js')

/**
 * Calibre Import Utilities
 * Reads Calibre's metadata.db and extracts book information
 */

class CalibreImporter {
  constructor(calibreLibraryPath) {
    this.libraryPath = calibreLibraryPath
    this.metadataDbPath = path.join(calibreLibraryPath, 'metadata.db')
    this.calibreDb = null
  }

  /**
   * Validate that the path is a valid Calibre library
   */
  validateLibrary() {
    console.log('[CalibreImporter] Validating library path:', this.libraryPath)
    console.log('[CalibreImporter] Checking if path exists:', fs.existsSync(this.libraryPath))
    
    if (!fs.existsSync(this.libraryPath)) {
      throw new Error(`Library path does not exist: ${this.libraryPath}`)
    }

    console.log('[CalibreImporter] Checking metadata.db at:', this.metadataDbPath)
    console.log('[CalibreImporter] metadata.db exists:', fs.existsSync(this.metadataDbPath))
    
    if (!fs.existsSync(this.metadataDbPath)) {
      throw new Error('metadata.db not found. This does not appear to be a Calibre library.')
    }

    return true
  }

  /**
   * Open connection to Calibre's metadata.db
   */
  connect() {
    try {
      this.calibreDb = new Database(this.metadataDbPath, { readonly: true })
      return true
    } catch (error) {
      throw new Error(`Failed to open Calibre database: ${error.message}`)
    }
  }

  /**
   * Close the database connection
   */
  close() {
    if (this.calibreDb) {
      this.calibreDb.close()
      this.calibreDb = null
    }
  }

  /**
   * Get total book count
   */
  getBookCount() {
    const result = this.calibreDb.prepare('SELECT COUNT(*) as count FROM books').get()
    return result.count
  }

  /**
   * Get all books with metadata
   */
  getAllBooks() {
    const books = this.calibreDb.prepare(`
      SELECT 
        id,
        title,
        sort as title_sort,
        timestamp,
        pubdate,
        series_index,
        isbn,
        path,
        has_cover,
        last_modified
      FROM books
      ORDER BY id
    `).all()

    // Enrich each book with related data
    return books.map(book => this.enrichBookData(book))
  }

  /**
   * Get books in batches for memory efficiency
   */
  getBooksBatch(offset, limit) {
    const books = this.calibreDb.prepare(`
      SELECT 
        id,
        title,
        sort as title_sort,
        timestamp,
        pubdate,
        series_index,
        isbn,
        path,
        has_cover,
        last_modified
      FROM books
      ORDER BY id
      LIMIT ? OFFSET ?
    `).all(limit, offset)

    return books.map(book => this.enrichBookData(book))
  }

  /**
   * Enrich book data with authors, tags, series, formats, etc.
   */
  enrichBookData(book) {
    const bookId = book.id

    // Get authors
    const authors = this.calibreDb.prepare(`
      SELECT a.name
      FROM authors a
      JOIN books_authors_link bal ON a.id = bal.author
      WHERE bal.book = ?
      ORDER BY bal.id
    `).all(bookId)

    // Get tags
    const tags = this.calibreDb.prepare(`
      SELECT t.name
      FROM tags t
      JOIN books_tags_link btl ON t.id = btl.tag
      WHERE btl.book = ?
    `).all(bookId)

    // Get series
    const series = this.calibreDb.prepare(`
      SELECT s.name, s.sort
      FROM series s
      JOIN books_series_link bsl ON s.id = bsl.series
      WHERE bsl.book = ?
    `).get(bookId)

    // Get publisher
    const publisher = this.calibreDb.prepare(`
      SELECT p.name
      FROM publishers p
      JOIN books_publishers_link bpl ON p.id = bpl.publisher
      WHERE bpl.book = ?
    `).get(bookId)

    // Get rating
    const rating = this.calibreDb.prepare(`
      SELECT r.rating
      FROM ratings r
      JOIN books_ratings_link brl ON r.id = brl.rating
      WHERE brl.book = ?
    `).get(bookId)

    // Get comments/description
    const comment = this.calibreDb.prepare(`
      SELECT text
      FROM comments
      WHERE book = ?
    `).get(bookId)

    // Get languages
    const languages = this.calibreDb.prepare(`
      SELECT l.lang_code
      FROM languages l
      JOIN books_languages_link bll ON l.id = bll.lang_code
      WHERE bll.book = ?
    `).all(bookId)

    // Get formats (epub, pdf, mobi, etc.)
    const formats = this.calibreDb.prepare(`
      SELECT format, name, uncompressed_size
      FROM data
      WHERE book = ?
    `).all(bookId)

    // Get identifiers (isbn, amazon, google, etc.)
    const identifiers = this.calibreDb.prepare(`
      SELECT type, val
      FROM identifiers
      WHERE book = ?
    `).all(bookId)

    // Build complete book object
    return {
      calibre_id: bookId,
      title: book.title,
      title_sort: book.title_sort,
      authors: authors.map(a => a.name).join(', '),
      tags: tags.map(t => t.name).join(', '),
      series_name: series ? series.name : null,
      series_index: book.series_index,
      publisher: publisher ? publisher.name : null,
      rating: rating ? rating.rating / 2 : null, // Calibre stores 0-10, we use 0-5
      isbn: book.isbn,
      description: comment ? comment.text : null,
      languages: languages.map(l => l.lang_code).join(', '),
      pubdate: book.pubdate,
      timestamp: book.timestamp,
      last_modified: book.last_modified,
      path: book.path, // Relative path in Calibre library
      has_cover: book.has_cover === 1,
      formats: formats,
      identifiers: identifiers.reduce((acc, id) => {
        acc[id.type] = id.val
        return acc
      }, {})
    }
  }

  /**
   * Get the file system path for a book's files
   */
  getBookPath(book) {
    return path.join(this.libraryPath, book.path)
  }

  /**
   * Get cover image path
   */
  getCoverPath(book) {
    if (!book.has_cover) return null
    return path.join(this.getBookPath(book), 'cover.jpg')
  }

  /**
   * Get format file path
   */
  getFormatPath(book, format) {
    const bookPath = this.getBookPath(book)
    const formatData = book.formats.find(f => f.format.toUpperCase() === format.toUpperCase())
    if (!formatData) return null
    return path.join(bookPath, `${formatData.name}.${formatData.format.toLowerCase()}`)
  }

  /**
   * Get metadata.opf path (optional additional metadata)
   */
  getMetadataOpfPath(book) {
    return path.join(this.getBookPath(book), 'metadata.opf')
  }

  /**
   * Parse metadata.opf file for additional info
   */
  async parseMetadataOpf(book) {
    const opfPath = this.getMetadataOpfPath(book)
    if (!fs.existsSync(opfPath)) return null

    try {
      const xml = fs.readFileSync(opfPath, 'utf8')
      const result = await parseStringPromise(xml)
      return result
    } catch (error) {
      console.error(`Error parsing metadata.opf for book ${book.calibre_id}:`, error.message)
      return null
    }
  }

  /**
   * Get custom columns (reading progress, custom fields, etc.)
   */
  getCustomColumns() {
    try {
      const columns = this.calibreDb.prepare(`
        SELECT 
          id,
          label,
          name,
          datatype,
          display
        FROM custom_columns
      `).all()

      return columns
    } catch (error) {
      // Table might not exist in older Calibre versions
      return []
    }
  }

  /**
   * Get custom column data for a book
   */
  getCustomColumnData(bookId, columnId, datatype) {
    try {
      let tableName = `custom_column_${columnId}`
      const data = this.calibreDb.prepare(`
        SELECT value
        FROM ${tableName}
        WHERE book = ?
      `).get(bookId)

      return data ? data.value : null
    } catch (error) {
      return null
    }
  }
}

module.exports = { CalibreImporter }
