import { defineConfig } from "orval"

/**
 * Generate React Query hooks from frontend/openapi.json.
 * Refresh from the running .NET API, then run: npm run generate:api
 *
 *   curl -o openapi.json http://localhost:8000/swagger/v1/swagger.json
 *
 * Do not set both useQuery and useMutation to true globally — in Orval 8
 * that inverts hook kinds (GET → mutation, POST → query). Leave defaults:
 * GET → useQuery, other verbs → useMutation.
 */
export default defineConfig({
  trippy: {
    input: {
      target: "./openapi.json",
    },
    output: {
      mode: "tags-split",
      target: "src/api/generated/endpoints.ts",
      schemas: "src/api/generated/models",
      client: "react-query",
      httpClient: "axios",
      clean: true,
      override: {
        mutator: {
          path: "./src/api/mutator.ts",
          name: "customInstance",
        },
      },
    },
  },
})
