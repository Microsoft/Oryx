FROM oryxdevmcr.azurecr.io/public/oryx/build AS main
ARG SDK_STORAGE_ENV_NAME
ARG SDK_STORAGE_BASE_URL_VALUE
ARG AI_KEY
ARG GIT_COMMIT=unspecified
ARG BUILD_NUMBER=unspecified
ARG RELEASE_TAG_NAME=unspecified

RUN rm -rf /tmp/oryx

ENV PATH="$ORIGINAL_PATH:$ORYX_PATHS"
ENV ORYX_AI_INSTRUMENTATION_KEY=${AI_KEY}
ENV ${SDK_STORAGE_ENV_NAME} ${SDK_STORAGE_BASE_URL_VALUE}
ENV ORYX_PREFER_USER_INSTALLED_SDKS=true
LABEL com.microsoft.oryx.git-commit=${GIT_COMMIT}
LABEL com.microsoft.oryx.build-number=${BUILD_NUMBER}
LABEL com.microsoft.oryx.release-tag-name=${RELEASE_TAG_NAME}