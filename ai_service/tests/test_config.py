from __future__ import annotations

from app.config import Settings


def test_settings_use_qwen_2_5_7b_by_default(monkeypatch):
    monkeypatch.delenv("AI_SERVICE_OLLAMA_MODEL", raising=False)

    settings = Settings.from_env()

    assert settings.ollama_model == "qwen2.5:7b"


def test_settings_allow_ollama_model_override(monkeypatch):
    monkeypatch.setenv("AI_SERVICE_OLLAMA_MODEL", "custom-model")

    settings = Settings.from_env()

    assert settings.ollama_model == "custom-model"


def test_settings_use_groq_api_by_default(monkeypatch):
    monkeypatch.delenv("AI_PROVIDER", raising=False)
    monkeypatch.delenv("AI_API_KEY", raising=False)
    monkeypatch.delenv("GROQ_API_KEY", raising=False)
    monkeypatch.delenv("GROQ_QWEN_API", raising=False)
    monkeypatch.delenv("AI_BASE_URL", raising=False)
    monkeypatch.delenv("GROQ_BASE_URL", raising=False)
    monkeypatch.delenv("AI_MODEL", raising=False)
    monkeypatch.delenv("GROQ_MODEL", raising=False)

    settings = Settings.from_env()

    assert settings.ai_provider == "groq"
    assert settings.ai_base_url == "https://api.groq.com/openai/v1"
    assert settings.ai_model == "qwen/qwen3-32b"


def test_settings_allow_groq_overrides(monkeypatch):
    monkeypatch.setenv("GROQ_QWEN_API", "test-key")
    monkeypatch.setenv("GROQ_BASE_URL", "https://example.test/openai/v1")
    monkeypatch.setenv("GROQ_MODEL", "custom-qwen")

    settings = Settings.from_env()

    assert settings.ai_api_key == "test-key"
    assert settings.ai_base_url == "https://example.test/openai/v1"
    assert settings.ai_model == "custom-qwen"
