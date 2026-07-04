"""FinTech transaction processing pipeline.

Three sequential, framework-free stages (`validator`, `fraud_detector`,
`report`) that move JSON envelope files through the `shared/` directory
contract described in specification.md. Each stage exposes a pure
`run(...)` function with injectable directories so it can be exercised in
isolation without the FastAPI dashboard or a live filesystem layout beyond
plain `pathlib.Path` objects (Clean Architecture: no stage imports FastAPI,
uvicorn, or anything from `frontend/`).
"""
