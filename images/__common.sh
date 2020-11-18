__CURRENT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $__CURRENT_DIR/__sdkStorageConstants.sh

function downloadFileAndVerifyChecksum() {
    local platformName="$1"
    local version="$2"
    local downloadedFileName="$3"
    local downloadableFileName="$3"
    local headersFile="/tmp/headers.txt"

    echo "Downloading $platformName version '$version'..."
    curl \
        -D $headersFile \
        -SL "$DEV_SDK_STORAGE_BASE_URL/$platformName/$downloadableFileName" \
        --output $downloadedFileName

    # Use all lowercase letters to find the header and it's value
    headerName="x-ms-meta-checksum"
    # Search the header ignoring case
    checksumHeader=$(cat $headersFile | grep -i $headerName: | tr -d '\r')
    # Change the found header and value to lowercase
    checksumHeader=$(echo $checksumHeader | tr '[A-Z]' '[a-z]')
    checksumValue=${checksumHeader#"$headerName: "}
    rm -f $headersFile
    echo
    echo "Verifying checksum..."
    echo "$checksumValue $downloadedFileName" | sha512sum -c -
}