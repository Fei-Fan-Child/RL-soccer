# ============================================================
# TensorFlow Serving 镜像
# 基于官方 TF Serving 镜像，预装 Soccer RL 模型
# ============================================================

FROM tensorflow/serving:2.17.0

# 将模型拷贝到容器内
COPY ./models /models

# TF Serving 默认配置
ENV MODEL_NAME=soccer_twos
ENV MODEL_BASE_PATH=/models

# 暴露 gRPC 端口 (8500) 和 REST API 端口 (8501)
EXPOSE 8500 8501

# 启动 TF Serving
ENTRYPOINT ["tensorflow_model_server", \
    "--rest_api_port=8501", \
    "--grpc_port=8500", \
    "--model_config_file=/models/models.config"]
