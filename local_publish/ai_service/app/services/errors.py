class DraftValidationError(Exception):
    """Raised when the generated draft is invalid."""


class DraftNotFoundError(FileNotFoundError):
    """Raised when a draft cannot be found in storage."""


class AiProviderUnavailableError(RuntimeError):
    """Raised when an AI provider cannot answer a request."""


class OllamaUnavailableError(AiProviderUnavailableError):
    """Backward-compatible alias for old Ollama errors."""


class DatabaseUnavailableError(RuntimeError):
    """Raised when PostgreSQL is unavailable."""
