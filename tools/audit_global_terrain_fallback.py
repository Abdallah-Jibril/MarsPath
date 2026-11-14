"""Read-only comparison of shipped official MOLA rasters outside Jezero.

Run with the QGIS Python launcher (NumPy). No Unity or network is required.
The independently sampled profiles quantify what the former 16 ppd-only
context path discarded even before a regional stereo DEM becomes resident.
"""
import json
from pathlib import Path

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "MarsPathUnity/Assets"
NAMES = ("megt90n000gb", "megt90n180gb", "megt00n000gb", "megt00n180gb")
QUADS = [np.memmap(DATA / f"StreamingAssets/Mola64/{name}.img", dtype=">i2", mode="r", shape=(5760, 11520)) for name in NAMES]
COARSE = np.memmap(DATA / "Resources/NASA/MarsMola16.bytes", dtype=">i2", mode="r", shape=(2880, 5760))


def elevation(lat, lon, ppd):
    lat, lon = np.broadcast_arrays(np.asarray(lat), np.asarray(lon))
    column = np.remainder(lon, 360) * ppd - .5
    row = np.clip((90 - lat) * ppd - .5, 0, 180 * ppd - 1)
    rawx, y = np.floor(column).astype(int), np.floor(row).astype(int)
    x, x1 = rawx % (360 * ppd), (rawx + 1) % (360 * ppd)
    y1 = np.minimum(y + 1, 180 * ppd - 1)

    def read(cx, cy):
        if ppd == 16:
            return COARSE[cy, cx].astype(float)
        tile = (cy // 5760) * 2 + cx // 11520
        result = np.empty(cx.shape, dtype=float)
        for i, quad in enumerate(QUADS):
            mask = tile == i
            result[mask] = quad[cy[mask] % 5760, cx[mask] % 11520]
        return result

    tx, ty = column - rawx, row - y
    return ((1 - tx) * read(x, y) + tx * read(x1, y)) * (1 - ty) + ((1 - tx) * read(x, y1) + tx * read(x1, y1)) * ty


def geographic_grid(latitude, longitude):
    x, z = np.meshgrid(np.arange(-60000, 60001, 2000), np.arange(-60000, 60001, 2000))
    valid = x * x + z * z <= 60000 ** 2
    x, z = x[valid], z[valid]
    radius = np.hypot(x, z)
    angle = radius / 3396000
    east, north = x / np.maximum(radius, 1e-12), z / np.maximum(radius, 1e-12)
    lat0, lon0 = np.radians(latitude), np.radians(longitude)
    lat = np.arcsin(np.sin(lat0) * np.cos(angle) + np.cos(lat0) * np.sin(angle) * north)
    lon = lon0 + np.arctan2(np.sin(angle) * east, np.cos(lat0) * np.cos(angle) - np.sin(lat0) * np.sin(angle) * north)
    return np.degrees(lat), np.degrees(lon)


def main():
    locations = (
        ("Gale", -5.4, 137.8),
        ("Valles Marineris", -13.9, -59.2),
        ("Olympus Mons", 18.65, -133.8),
        ("Arabia Terra", 20.99, 22.478),
        ("Elysium Planitia", 4.5, 135.9),
        ("Utopia Planitia", 46.7, 117.5),
        ("Dateline", 10, 179.99),
    )
    report = {"source": "Bundled official PDS MOLA 64 ppd and 16 ppd; independent native big-endian raster sampling", "radius_m": 60000, "physical_height_scale": 1, "locations": []}
    for name, latitude, longitude in locations:
        lat, lon = geographic_grid(latitude, longitude)
        fine, coarse = elevation(lat, lon, 64), elevation(lat, lon, 16)
        wrap_error = np.max(np.abs(fine - elevation(lat, lon + 360, 64)))
        assert np.isfinite(fine).all() and wrap_error < 1e-6
        report["locations"].append({"name": name, "latitude": latitude, "longitude": longitude, "samples": len(lat), "native_min_m": round(float(fine.min()), 3), "native_max_m": round(float(fine.max()), 3), "native_relief_m": round(float(np.ptp(fine)), 3), "old_16ppd_max_height_error_m": round(float(np.max(np.abs(fine - coarse))), 3), "longitude_wrap_error_m": float(wrap_error)})
    destination = ROOT / "tmp/global-terrain-fallback-audit-20261108.json"
    destination.parent.mkdir(exist_ok=True)
    destination.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
