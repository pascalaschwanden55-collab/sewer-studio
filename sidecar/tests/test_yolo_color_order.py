from __future__ import annotations

import numpy as np
from PIL import Image

from sidecar.models import yolo_wrapper


def test_pil_rgb_is_passed_to_ultralytics_as_contiguous_bgr() -> None:
    image = Image.new("RGB", (2, 1))
    image.putdata([(10, 20, 30), (200, 150, 100)])

    source = yolo_wrapper._pil_rgb_to_ultralytics_bgr(image)

    assert source.dtype == np.uint8
    assert source.flags.c_contiguous
    assert source.tolist() == [[[30, 20, 10], [100, 150, 200]]]

