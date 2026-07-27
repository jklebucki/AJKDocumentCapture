FROM python:3.12.11-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PIP_NO_CACHE_DIR=1
RUN apt-get update && apt-get install -y --no-install-recommends curl libgl1 libglib2.0-0 libgomp1 && rm -rf /var/lib/apt/lists/*
RUN python -m pip install --upgrade pip \
 && python -m pip install "paddlepaddle==3.2.1" -i https://www.paddlepaddle.org.cn/packages/stable/cpu/ \
 && python -m pip install "paddleocr[doc-parser]==3.7.0" "paddlex[ocr]==3.7.2" \
 && paddlex --install serving
COPY pipeline_config.yml /opt/paddleocr/pipeline_config.yml
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=180s --retries=5 CMD curl -fsS http://localhost:8080/health || exit 1
CMD ["paddlex", "--serve", "--pipeline", "/opt/paddleocr/pipeline_config.yml", "--device", "cpu"]
