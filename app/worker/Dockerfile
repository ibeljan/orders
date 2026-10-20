# syntax=docker/dockerfile:1
FROM python:3.13-slim
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1
# dependencies first → cached until requirements.txt changes
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY worker/ worker/
RUN useradd --uid 1000 --no-create-home --shell /usr/sbin/nologin app
USER 1000
# the worker has no HTTP endpoint: healthy = heartbeat file touched in the last 60 s
HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
  CMD python -c "import os,sys,time; sys.exit(0 if time.time()-os.path.getmtime('/tmp/worker-heartbeat') < 60 else 1)"
CMD ["python", "-m", "worker.main"]
