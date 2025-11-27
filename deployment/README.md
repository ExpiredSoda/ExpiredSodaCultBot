# Deployment Files

This folder contains optional deployment configurations. **Railway doesn't need these** - it builds .NET apps natively!

## Docker Deployment (Optional)

If you want to use Docker instead of Railway:

1. Copy these files to the root directory:
   ```bash
   cp deployment/Dockerfile ../
   cp deployment/docker-compose.yml ../
   ```

2. Follow the Docker instructions in the main README.

## Files:
- `Dockerfile` - Container configuration
- `docker-compose.yml` - Easy Docker deployment
