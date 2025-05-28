# How to Run Gatekeeper Rate Limiter

This guide will help you build, run, and test the Gatekeeper distributed rate limiting service.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Redis and containerized deployment)
- [Redis](https://redis.io/download) (if running locally without Docker)

## Quick Start with Docker (Recommended)

### 1. Build and Run Everything with Docker Compose

```bash
# Clone and navigate to the project
cd Gatekeeper

# Build and start all services (API + Redis)
docker-compose up --build

# Or run in detached mode
docker-compose up --build -d
```

This will start:
- **Redis** on `localhost:6379`
- **Gatekeeper API** on `localhost:5000`
- **Gatekeeper gRPC** on `localhost:5001`
- **Gatekeeper GraphQL** on `localhost:5002`

## Local Development Setup

### 1. Start Redis

**Option A: Using Docker**
```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine
```

**Option B: Install Redis locally**
- Follow [Redis installation guide](https://redis.io/docs/install/) for your OS
- Start Redis: `redis-server`

### 2. Build the Solution

```bash
# Build all projects
dotnet build

# Or build and run specific project
dotnet run --project src/Gatekeeper.Api
```

### 3. Configuration

The API uses the following configuration sources (in order of precedence):

1. **Environment Variables**
2. **appsettings.json**
3. **Default values**

#### Environment Variables

```bash
# Redis connection string
export ConnectionStrings__Redis="localhost:6379"

# Or for development
export ASPNETCORE_ENVIRONMENT="Development"
```

#### appsettings.json Example

Create or update `src/Gatekeeper.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Gatekeeper": "Debug"
    }
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "AllowedHosts": "*"
}
```

## Testing the Rate Limiter

### Using curl

#### 1. Check Rate Limit (doesn't consume)

```bash
curl -X POST http://localhost:5000/check \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user123",
    "route": "/api/orders",
    "limitId": "ORDERS-PER-MINUTE"
  }'
```

**Expected Response:**
```json
{
  "allowed": true,
  "remaining": 10,
  "resetIn": "00:00:45"
}
```

#### 2. Consume Rate Limit (decrements counter)

```bash
curl -X POST http://localhost:5000/consume \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user123",
    "route": "/api/orders",
    "limitId": "ORDERS-PER-MINUTE"
  }'
```

**Expected Response:**
```json
{
  "allowed": true,
  "remaining": 9,
  "resetIn": "00:00:44"
}
```

#### 3. Test Rate Limiting

Run the consume command 10 times rapidly:

```bash
# Bash script to test rate limiting
for i in {1..12}; do
  echo "Request $i:"
  curl -X POST http://localhost:5000/consume \
    -H "Content-Type: application/json" \
    -d '{
      "userId": "test-user",
      "route": "/api/test",
      "limitId": "TEST-LIMIT"
    }' && echo
  sleep 0.1
done
```

After 10 requests, you should see:
```json
{
  "allowed": false,
  "remaining": 0,
  "resetIn": "00:00:XX"
}
```

### Using Postman

1. **Import Collection**: Create a new collection with these requests:

2. **Check Endpoint**
   - Method: `POST`
   - URL: `http://localhost:5000/check`
   - Headers: `Content-Type: application/json`
   - Body (raw JSON):
     ```json
     {
       "userId": "user123",
       "route": "/api/orders",
       "limitId": "ORDERS-PER-MINUTE"
     }
     ```

3. **Consume Endpoint**
   - Method: `POST`
   - URL: `http://localhost:5000/consume`
   - Headers: `Content-Type: application/json`
   - Body (raw JSON):
     ```json
     {
       "userId": "user123",
       "route": "/api/orders",
       "limitId": "ORDERS-PER-MINUTE"
     }
     ```

## Rate Limiting Configuration

### Current Settings

- **Window Size**: 60 seconds (fixed window)
- **Max Requests**: 10 requests per window
- **Key Format**: `rate:{limitId}:{userId}:{window}`

### Understanding the Response

```json
{
  "allowed": true,      // Whether the request is allowed
  "remaining": 7,       // Requests remaining in current window
  "resetIn": "00:00:45" // Time until window resets
}
```

## Redis Connection

### Verify Redis Connection

```bash
# Connect to Redis CLI (if running locally)
redis-cli

# Test connection
127.0.0.1:6379> ping
PONG

# View rate limit keys
127.0.0.1:6379> keys rate:*

# Check specific user's limit
127.0.0.1:6379> get "rate:ORDERS-PER-MINUTE:user123:12345"
```

### Docker Redis Access

```bash
# Connect to Redis in Docker container
docker exec -it gatekeeper-redis-1 redis-cli

# Or if you named it differently
docker ps  # Find Redis container name
docker exec -it <redis-container-name> redis-cli
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "RedisRateLimiterTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Common Issues

1. **Redis Connection Failed**
   ```
   Error: "No connection is available to service this operation"
   ```
   - Ensure Redis is running on `localhost:6379`
   - Check firewall settings
   - Verify connection string in configuration

2. **Port Already in Use**
   ```
   Error: "Address already in use"
   ```
   - Change port in `launchSettings.json` or docker-compose
   - Kill existing processes: `lsof -ti:5000 | xargs kill -9`

3. **Docker Build Issues**
   ```bash
   # Clean Docker cache
   docker system prune -f
   
   # Rebuild without cache
   docker-compose build --no-cache
   ```

### Debug Mode

Run with debug logging:

```bash
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/Gatekeeper.Api
```

### Health Checks

```bash
# API Health
curl http://localhost:5000/ping

# Redis Health
redis-cli ping
```

## Environment Variables Reference

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__Redis` | Redis connection string | `localhost:6379` |
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | `Production` |
| `ASPNETCORE_URLS` | URLs to bind to | `http://+:8080` |

## Next Steps

- Check out the [Architecture Overview](../CLAUDE.md) for implementation details
- Explore the GraphQL endpoint at `http://localhost:5002/graphql` (when implemented)
- Review the gRPC service definitions in `src/Gatekeeper.Grpc/Protos/`
- Set up monitoring and alerting for production use