"""Gemeinsames Predict-Lock des geteilten GPU-Platzes ``YOLO_TEST``.

Der BCC-Pilot (`bcc_test_wrapper`) und die Lernstufen-Klassifikation
(`lernstufe_wrapper`) laden ihre Kandidaten in DENSELBEN Slot. Vorher besass jedes
Modul ein eigenes Lock; damit konnten sich ihre Folgen aus Laden und Inferenz
verschraenken und der eine Wrapper inferierte auf dem Modell des anderen
(Audit 2026-08-14, Befund S-H1).

Ein gemeinsames Lock serialisiert beide Wrapper vollstaendig gegeneinander. Die
Sperrenreihenfolge aus `gpu_manager` bleibt unveraendert: dieses Wrapper-Lock liegt
immer AUSSEN, danach folgen Slot-Lock und `_global_lock`.

Reentrant (RLock), damit ein Wrapper unter dem Lock gefahrlos einen Helfer aufrufen
kann, der es erneut anfordert.
"""

import threading

PREDICT_LOCK = threading.RLock()
