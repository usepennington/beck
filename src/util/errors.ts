/** An error with an author-friendly message about their diagram source. */
export class BeckError extends Error {
  /** Optional 1-based line in the YAML source where the problem is. */
  line?: number

  constructor(message: string, line?: number) {
    super(line != null ? `${message} (line ${line})` : message)
    this.name = 'BeckError'
    this.line = line
  }
}
