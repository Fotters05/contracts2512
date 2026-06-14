from __future__ import annotations

import json
import re
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from app.config import Settings
from app.services.errors import AiProviderUnavailableError


class AiProviderService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def check_health(self) -> bool:
        if not self._settings.ai_api_key:
            return False

        try:
            request = Request(
                f"{self._settings.ai_base_url.rstrip('/')}/models",
                headers={
                    "Authorization": f"Bearer {self._settings.ai_api_key}",
                    "Accept": "application/json",
                    "User-Agent": "contracts2512-ai-service/1.0",
                },
                method="GET",
            )
            with urlopen(request, timeout=5) as response:
                response.read()
            return True
        except Exception:
            return False

    def generate_text(self, prompt: str, system_prompt: str | None = None) -> str:
        if not self._settings.ai_api_key:
            raise AiProviderUnavailableError(
                "API-ключ ИИ не задан. Укажите GROQ_QWEN_API, GROQ_API_KEY или AI_API_KEY в .env."
            )

        payload = self._build_payload(prompt, system_prompt, include_reasoning_effort=True)
        try:
            data = self._post_chat_completion(payload)
        except AiProviderUnavailableError as exc:
            if "reasoning_effort" not in str(exc):
                raise
            data = self._post_chat_completion(
                self._build_payload(prompt, system_prompt, include_reasoning_effort=False)
            )

        text = self._extract_text(data)
        text = self._clean_text(text)
        if not text:
            raise AiProviderUnavailableError("API ИИ вернул пустой ответ.")
        return text

    def generate_json(self, prompt: str, system_prompt: str | None = None) -> dict[str, Any]:
        raw = self.generate_text(
            "Верни только корректный JSON без пояснений, markdown и рассуждений.\n" + prompt,
            system_prompt=system_prompt,
        )
        start = raw.find("{")
        end = raw.rfind("}")
        if start == -1 or end == -1 or end < start:
            raise AiProviderUnavailableError("API ИИ не вернул корректный JSON.")
        try:
            return json.loads(raw[start : end + 1])
        except json.JSONDecodeError as exc:
            raise AiProviderUnavailableError("Не удалось разобрать JSON от API ИИ.") from exc

    def _build_payload(
        self,
        prompt: str,
        system_prompt: str | None,
        include_reasoning_effort: bool,
    ) -> dict[str, Any]:
        system = (
            system_prompt
            or "Ты помощник для генерации учебных документов. Отвечай только финальным текстом без рассуждений."
        )
        payload: dict[str, Any] = {
            "model": self._settings.ai_model,
            "messages": [
                {
                    "role": "system",
                    "content": (
                        f"{system}\n"
                        "Не показывай внутренние рассуждения, chain-of-thought, анализ, теги <think>."
                    ),
                },
                {"role": "user", "content": prompt},
            ],
            "temperature": 0.4,
            "max_completion_tokens": 4096,
            "top_p": 0.9,
            "stream": False,
        }
        if include_reasoning_effort:
            payload["reasoning_effort"] = "none"
        return payload

    def _post_chat_completion(self, payload: dict[str, Any]) -> dict[str, Any]:
        request = Request(
            f"{self._settings.ai_base_url.rstrip('/')}/chat/completions",
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {self._settings.ai_api_key}",
                "Content-Type": "application/json",
                "Accept": "application/json",
                "User-Agent": "contracts2512-ai-service/1.0",
            },
            method="POST",
        )
        try:
            with urlopen(request, timeout=self._settings.ai_timeout_seconds) as response:
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise AiProviderUnavailableError(f"API ИИ вернул ошибку {exc.code}: {detail}") from exc
        except URLError as exc:
            raise AiProviderUnavailableError("Не удалось подключиться к API ИИ.") from exc
        except Exception as exc:
            raise AiProviderUnavailableError("Не удалось получить ответ от API ИИ.") from exc

    def _extract_text(self, data: dict[str, Any]) -> str:
        try:
            return str(data["choices"][0]["message"]["content"]).strip()
        except (KeyError, IndexError, TypeError) as exc:
            raise AiProviderUnavailableError("API ИИ вернул неожиданный формат ответа.") from exc

    def _clean_text(self, text: str) -> str:
        text = re.sub(r"<think>.*?</think>", "", text, flags=re.IGNORECASE | re.DOTALL)
        text = re.sub(r"^```(?:json|text)?\s*", "", text.strip(), flags=re.IGNORECASE)
        text = re.sub(r"\s*```$", "", text.strip())
        return text.strip()
