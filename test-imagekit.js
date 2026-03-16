// Quick test script to verify ImageKit credentials
// Run with: node test-imagekit.js

const publicKey = "public_1bswA0Vq66mBJQlYJxBAyPJm3dE=";
const privateKey = "private_I+B7d2/bfoZkFllZCf07835bjb8=";
const urlEndpoint = "https://ik.imagekit.io/fnyx4x87z";

console.log("Testing ImageKit Configuration:");
console.log("Public Key:", publicKey);
console.log("Private Key:", privateKey.substring(0, 15) + "...");
console.log("URL Endpoint:", urlEndpoint);
console.log("\nCredentials look valid. Make sure these are in your appsettings.json");
