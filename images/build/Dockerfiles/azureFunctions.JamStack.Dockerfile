FROM oryxdevmcr.azurecr.io/public/oryx/build:github-actions AS main

ENV ORYX_BUILDIMAGE_TYPE="jamstack" \
    PATH="/usr/local/go/bin:$PATH"

COPY --from=support-files-image-for-build /tmp/oryx/ /tmp
RUN oryx prep --skip-detection --platforms-and-versions nodejs=12 \
    && echo "jamstack" > /opt/oryx/.imagetype \
    && . /tmp/build/__goVersions.sh \
    && downloadedFileName="go${GO_VERSION}.linux-amd64.tar.gz" \
    && curl -SLsO https://golang.org/dl/$downloadedFileName \
    && tar -C /usr/local -xzf $downloadedFileName \
    && rm -rf $downloadedFileName
