#import <AVFoundation/AVFoundation.h>

extern "C" int WindEnergyCameraAuthorizationStatus()
{
    switch ([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo]) {
        case AVAuthorizationStatusNotDetermined: return 0;
        case AVAuthorizationStatusRestricted: return 1;
        case AVAuthorizationStatusDenied: return 2;
        case AVAuthorizationStatusAuthorized: return 3;
        default: return 0;
    }
}
