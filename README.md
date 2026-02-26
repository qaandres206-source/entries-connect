# EntriesConnect app

## Run the app

### uv

Run as a desktop app:

```
uv run flet run
```

Run as a web app:

```
uv run flet run --web
```

### Poetry

Install dependencies from `pyproject.toml`:

```
poetry install
```

Run as a desktop app:

```
poetry run flet run
```

Run as a web app:

```
poetry run flet run --web
```

For more details on running the app, refer to the [Getting Started Guide](https://flet.dev/docs/getting-started/).

## Build the app

### Android

```
flet build apk -v
```

For more details on building and signing `.apk` or `.aab`, refer to the [Android Packaging Guide](https://flet.dev/docs/publish/android/).

### iOS

```
flet build ipa -v
```

For more details on building and signing `.ipa`, refer to the [iOS Packaging Guide](https://flet.dev/docs/publish/ios/).

### macOS

```
flet build macos -v
```

For more details on building macOS package, refer to the [macOS Packaging Guide](https://flet.dev/docs/publish/macos/).

### Linux

```
flet build linux -v
```

For more details on building Linux package, refer to the [Linux Packaging Guide](https://flet.dev/docs/publish/linux/).

### Windows

```
flet build windows -v
```

For more details on building Windows package, refer to the [Windows Packaging Guide](https://flet.dev/docs/publish/windows/).

## Azure DevOps Deployment

This project uses Azure Pipelines for CI/CD. The configuration is found in [`azure-pipelines.yml`](./azure-pipelines.yml).

### Azure Setup Requirements

To deploy this application to Azure App Service properly, you need to create a **Linux Web App** in Azure with the following settings:
1.  **Runtime Stack**: `Python 3.11`
2.  **OS**: Linux
3.  **Startup Command**: Under **Configuration > General settings**, change the startup command to:
    ```bash
    python main.py
    ```

### Pipeline Variables Configuration
In Azure DevOps, when editing the pipeline, make sure you update the placeholder variables in `azure-pipelines.yml` or set them up in the pipeline variables UI:
- `azureServiceConnectionId`: Your Azure Resource Manager service connection.
- `webAppName`: The name of your Azure App Service.